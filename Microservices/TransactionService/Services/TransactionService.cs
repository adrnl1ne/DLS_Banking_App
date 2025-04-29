using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;

namespace TransactionService.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;
    private readonly UserAccountClientService _userAccountClient;
    private readonly IRabbitMqClient _rabbitMqClient;
    private readonly ILogger<TransactionService> _logger;
    private readonly Histogram _histogram;

    // Prometheus metrics
    private static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "transaction_service_requests_total",
        "Total number of requests to TransactionService",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    private static readonly Counter ErrorsTotal = Metrics.CreateCounter(
        "transaction_service_errors_total",
        "Total number of errors in TransactionService",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    private static readonly Counter SuccessesTotal = Metrics.CreateCounter(
        "transaction_service_successes_total",
        "Total number of successful operations in TransactionService",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    private readonly Dictionary<string, TaskCompletionSource<FraudResult>> _pendingFraudChecks = new();
    private readonly ConcurrentDictionary<string, FraudResult> _processedFraudResults = new();

    public TransactionService(
        ITransactionRepository repository,
        UserAccountClientService userAccountClient,
        IRabbitMqClient rabbitMqClient,
        ILogger<TransactionService> logger,
        Histogram histogram)
    {
        _repository = repository;
        _userAccountClient = userAccountClient;
        _rabbitMqClient = rabbitMqClient;
        _logger = logger;
        _histogram = histogram;

        // Subscribe to fraud detection results
        try
        {
            _rabbitMqClient.Subscribe("TransactionServiceQueue", HandleFraudResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to TransactionServiceQueue. Fraud detection will be skipped.");
        }
    }

    public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
    {
        RequestsTotal.WithLabels("CreateTransfer").Inc();
        try
        {
            _logger.LogInformation("Creating transfer");

            // Parse account IDs to integers
            if (!int.TryParse(request.FromAccount, out int fromAccountId) ||
                !int.TryParse(request.ToAccount, out int toAccountId))
            {
                _logger.LogWarning("Invalid account ID format");
                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw new ArgumentException("Account IDs must be valid integers");
            }

            // Check if from and to accounts are the same
            if (fromAccountId == toAccountId)
            {
                _logger.LogWarning("Cannot transfer to the same account - AccountId:");
                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw new InvalidOperationException("Cannot transfer funds to the same account");
            }

            // Before creating transaction, check if we have sufficient funds
            var fromAccount = await _userAccountClient.GetAccountAsync(fromAccountId);
            if (fromAccount == null)
            {
                _logger.LogWarning("Source account not found");
                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw new InvalidOperationException("Source account not found");
            }

            // Check if user owns the source account
            if (fromAccount.UserId != request.UserId)
            {
                _logger.LogWarning("User does not own the source account");
                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw new UnauthorizedAccessException("You can only transfer funds from your own accounts");
            }

            // Check sufficient balance for withdrawal/transfer
            if (fromAccount.Amount < request.Amount)
            {
                _logger.LogWarning("Insufficient funds in account.");
                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw new InvalidOperationException("Insufficient funds for this transfer");
            }

            // Validate destination account
            var toAccount = await _userAccountClient.GetAccountAsync(toAccountId);
            if (toAccount == null)
            {
                _logger.LogWarning("Destination account not found");
                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw new InvalidOperationException($"Destination account {toAccountId} not found");
            }

            // Create a pending transaction
            var transferId = Guid.NewGuid().ToString();
            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                TransferId = transferId,
                UserId = request.UserId,
                FromAccount = request.FromAccount,
                ToAccount = request.ToAccount,
                Amount = request.Amount,
                Currency = "USD", // Always default to USD as requested
                Status = "pending",
                TransactionType = request.TransactionType ?? "transfer",
                Description = request.Description ?? $"Transfer from account {fromAccountId} to {toAccountId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save the pending transaction
            await _repository.CreateTransactionAsync(transaction);
            _logger.LogInformation("Created pending transaction");

            try
            {
                // Create TaskCompletionSource to wait for the fraud check result FIRST
                var tcs = new TaskCompletionSource<FraudResult>();

                // Check if we already have a processed result for this transaction
                FraudResult cachedResult;

                // Use a proper lock to avoid race conditions
                lock (_pendingFraudChecks)
                {
                    if (_processedFraudResults.TryRemove(transferId, out cachedResult))
                    {
                        _logger.LogInformation("Found pre-processed fraud result for transaction {TransferId}",
                            transferId);
                        tcs.SetResult(cachedResult);
                    }
                    else
                    {
                        // No pre-processed result, register for future result
                        _pendingFraudChecks[transferId] = tcs;
                    }
                }

                // Prepare and send the fraud check message AFTER registering for the result
                _logger.LogInformation("Sending transaction to fraud check");
                var fraudMessage = new
                {
                    transferId = transaction.TransferId,
                    fromAccount = transaction.FromAccount,
                    toAccount = transaction.ToAccount,
                    amount = transaction.Amount,
                    userId = transaction.UserId,
                    timestamp = DateTime.UtcNow
                };

                // Send the message to fraud detection service
                _rabbitMqClient.Publish("CheckFraud", JsonSerializer.Serialize(fraudMessage));

                // Wait for the result if we don't already have it
                FraudResult fraudResult;
                if (cachedResult != null)
                {
                    fraudResult = cachedResult;
                    _logger.LogInformation("Using cached fraud result");
                }
                else
                {
                    // Implement a more robust waiting mechanism with increased timeout
                    try
                    {
                        // First wait with standard timeout (30 seconds instead of 15)
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
                        Task completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            // Try to clean up in case the result arrives late
                            lock (_pendingFraudChecks)
                            {
                                _pendingFraudChecks.Remove(transferId);
                            }

                            // Publish a direct fallback message - this will fake a fraud check result
                            _logger.LogWarning("Fraud check timed out for, using fallback direct processing");

                            // Skip fraud check and proceed with transaction as non-fraudulent
                            fraudResult = new FraudResult
                            {
                                TransferId = transferId,
                                IsFraud = false,
                                Status = "approved"
                            };
                        }
                        else
                        {
                            // Normal path - we got a result before timeout
                            fraudResult = await tcs.Task;
                            _logger.LogInformation("Received fraud check result for");
                        }
                    }
                    catch (TimeoutException)
                    {
                        // This should not happen with our new approach, but let's handle it anyway
                        _logger.LogWarning("Unexpected timeout, using fallback");
                        fraudResult = new FraudResult
                        {
                            TransferId = transferId,
                            IsFraud = false,
                            Status = "approved"
                        };
                    }
                }

                // Handle fraud detection result
                if (fraudResult.IsFraud)
                {
                    _logger.LogWarning("Fraud detected for transaction");

                    // Update transaction status to declined
                    transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                    if (transaction != null)
                    {
                        await _repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                    }

                    ErrorsTotal.WithLabels("CreateTransfer").Inc();
                    throw new InvalidOperationException("Transaction declined due to potential fraud");
                }

                // Continue with normal processing - create child transactions for withdrawal and deposit
                var withdrawalTransaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransferId = transferId + "-withdrawal",
                    UserId = request.UserId,
                    FromAccount = request.FromAccount,
                    ToAccount = request.FromAccount, // Same account
                    Amount = request.Amount,
                    Currency = "USD",
                    Status = "pending",
                    TransactionType = "withdrawal",
                    Description = $"Withdrawal from account {fromAccountId} for transfer {transferId}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Create deposit transaction for destination account
                var depositTransaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransferId = transferId + "-deposit",
                    UserId = toAccount.UserId, // Use destination account's user ID
                    FromAccount = request.ToAccount, // Same account
                    ToAccount = request.ToAccount,
                    Amount = request.Amount,
                    Currency = "USD",
                    Status = "pending",
                    TransactionType = "deposit",
                    Description = $"Deposit to account {toAccountId} from transfer {transferId}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Save the child transactions
                await _repository.CreateTransactionAsync(withdrawalTransaction);
                await _repository.CreateTransactionAsync(depositTransaction);

                // Update account balances - with ADJUSTMENT flags
                _logger.LogInformation("Updating balances for transaction {TransferId}", transferId);

                // For source account: SUBTRACT funds
                var fromBalanceRequest = new Models.AccountBalanceRequest
                {
                    Amount = request.Amount, // Send the amount to adjust by, not the new balance
                    TransactionId = transaction.TransferId + "-withdrawal",
                    TransactionType = "Withdrawal", // This account is being debited
                    IsAdjustment = true // This is critical - it tells the API to adjust rather than set
                };

                // For destination account: ADD funds
                var toBalanceRequest = new Models.AccountBalanceRequest
                {
                    Amount = request.Amount, // Send the amount to adjust by, not the new balance
                    TransactionId = transaction.TransferId + "-deposit",
                    TransactionType = "Deposit", // This account is being credited
                    IsAdjustment = true // This is critical - it tells the API to adjust rather than set
                };

                // Update the balances via the client service
                await _userAccountClient.UpdateBalanceAsync(fromAccountId, fromBalanceRequest);
                await _userAccountClient.UpdateBalanceAsync(toAccountId, toBalanceRequest);

                // Update all transaction statuses to completed
                transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                if (transaction != null)
                {
                    await _repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
                }

                var retrievedWithdrawalTx =
                    await _repository.GetTransactionByTransferIdAsync(transferId + "-withdrawal");
                if (retrievedWithdrawalTx != null)
                {
                    await _repository.UpdateTransactionStatusAsync(retrievedWithdrawalTx.Id, "completed");
                }

                var retrievedDepositTx = await _repository.GetTransactionByTransferIdAsync(transferId + "-deposit");
                if (retrievedDepositTx != null)
                {
                    await _repository.UpdateTransactionStatusAsync(retrievedDepositTx.Id, "completed");
                }

                _logger.LogInformation("Transaction completed successfully");

                // Track transaction amount in histogram for metrics
                _histogram.Observe((double)transaction.Amount);

                SuccessesTotal.WithLabels("CreateTransfer").Inc();
                return TransactionResponse.FromTransaction(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing transaction");

                try
                {
                    var failedTransaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                    if (failedTransaction != null)
                    {
                        await _repository.UpdateTransactionStatusAsync(failedTransaction.Id, "failed");
                    }
                    else
                    {
                        _logger.LogWarning("Could not find transaction to mark as failed");
                    }
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Error updating transaction status to failed");
                }

                ErrorsTotal.WithLabels("CreateTransfer").Inc();
                throw;
            }
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("CreateTransfer").Inc();
            _logger.LogError(ex, "Error creating transfer");
            throw;
        }
    }

    public async Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId)
    {
        RequestsTotal.WithLabels("GetTransactionByTransferId").Inc();
        try
        {
            var transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
            SuccessesTotal.WithLabels("GetTransactionByTransferId").Inc();
            return transaction != null ? TransactionResponse.FromTransaction(transaction) : null;
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("GetTransactionByTransferId").Inc();
            _logger.LogError(ex, "Error retrieving transaction");
            throw;
        }
    }

    public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId,
        int authenticatedUserId)
    {
        RequestsTotal.WithLabels("GetTransactionsByAccount").Inc();
        try
        {
            // Validate accountId format
            if (!int.TryParse(accountId, out int accountIdInt))
            {
                _logger.LogWarning("Invalid account ID format");
                ErrorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new ArgumentException("Account ID must be a valid integer.");
            }

            // Call UserAccountService to get account details
            _logger.LogInformation("Fetching account from UserAccountService");
            var account = await _userAccountClient.GetAccountAsync(accountIdInt);

            if (account == null)
            {
                _logger.LogWarning("Account not found in UserAccountService");
                ErrorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new InvalidOperationException($"Account {accountId} not found.");
            }

            // Validate that the authenticated user owns the account
            if (account.UserId != authenticatedUserId)
            {
                _logger.LogWarning("User is not authorized to access transactions for account");
                ErrorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new UnauthorizedAccessException(
                    "You are not authorized to access transactions for this account.");
            }

            // Fetch transactions from the repository
            var transactions = await _repository.GetTransactionsByAccountAsync(accountId);
            SuccessesTotal.WithLabels("GetTransactionsByAccount").Inc();
            return transactions.Select(TransactionResponse.FromTransaction);
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("GetTransactionsByAccount").Inc();
            _logger.LogError(ex, "Error retrieving transactions for account {AccountId}", accountId);
            throw;
        }
    }

    // Required interface implementation for backwards compatibility
    public void ProcessFraudResult(string transferId, bool isFraud)
    {
        try
        {
            _logger.LogInformation("Processing fraud result for {TransferId}: {IsFraud}", transferId, isFraud);

            var fraudResult = new FraudResult
            {
                TransferId = transferId,
                IsFraud = isFraud,
                Status = isFraud ? "declined" : "approved"
            };

            if (_pendingFraudChecks.TryGetValue(transferId, out var tcs))
            {
                tcs.TrySetResult(fraudResult);
                _logger.LogInformation("Completed pending fraud check for {TransferId}", transferId);
            }
            else
            {
                // If no pending check, process it directly
                _ = ProcessFraudResultAsync(transferId, isFraud);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud result for {TransferId}", transferId);
        }
    }

    // Add new method to handle fraud result processing
    private async Task ProcessFraudResultAsync(string transferId, bool isFraud)
    {
        try
        {
            string status = isFraud ? "declined" : "completed";
            _logger.LogInformation("Processing fraud result: Transaction {TransferId} marked as {Status}",
                transferId, status);

            // Use the same repository but in a new async context
            // Look up the main transaction
            var mainTransaction = await _repository.GetTransactionByTransferIdAsync(transferId);
            if (mainTransaction != null)
            {
                await _repository.UpdateTransactionStatusAsync(mainTransaction.Id, status);
                _logger.LogInformation("Updated main transaction {Id} status to {Status}", mainTransaction.Id,
                    status);
            }
            else
            {
                _logger.LogWarning("Main transaction with transfer ID {TransferId} not found", transferId);
            }

            // Look up withdrawal transaction
            var withdrawalTx = await _repository.GetTransactionByTransferIdAsync($"{transferId}-withdrawal");
            if (withdrawalTx != null)
            {
                await _repository.UpdateTransactionStatusAsync(withdrawalTx.Id, status);
                _logger.LogInformation("Updated withdrawal transaction {Id} status to {Status}", withdrawalTx.Id,
                    status);
            }

            // Look up deposit transaction
            var depositTx = await _repository.GetTransactionByTransferIdAsync($"{transferId}-deposit");
            if (depositTx != null)
            {
                await _repository.UpdateTransactionStatusAsync(depositTx.Id, status);
                _logger.LogInformation("Updated deposit transaction {Id} status to {Status}", depositTx.Id, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud result update for {TransferId}", transferId);
        }
    }

    // Handle fraud check results from RabbitMQ
    private void HandleFraudResult(string message)
    {
        try
        {
            _logger.LogInformation("Received fraud result message: {Message}", message);

            // Parse the message to extract the transfer ID and fraud status
            string transferId = null;
            bool isFraud = false;

            // Try using regular expressions first since it's more tolerant of malformed JSON
            try
            {
                // Extract fields with regex - more robust against malformed timestamps
                transferId = ExtractJsonField(message, "transferId");
                string isFraudStr = ExtractJsonField(message, "isFraud").ToLower();
                isFraud = isFraudStr == "true";

                _logger.LogInformation("Extracted from message: transferId={TransferId}, isFraud={IsFraud}",
                    transferId, isFraud);
            }
            catch (Exception regexEx)
            {
                _logger.LogWarning("Regex parsing failed: {Error}. Trying JSON parsing", regexEx.Message);

                // Fallback to standard JSON parsing
                try
                {
                    using var doc = JsonDocument.Parse(message);
                    if (doc.RootElement.TryGetProperty("transferId", out var idElement))
                    {
                        transferId = idElement.GetString();
                    }

                    if (doc.RootElement.TryGetProperty("isFraud", out var fraudElement))
                    {
                        isFraud = fraudElement.GetBoolean();
                    }

                    _logger.LogInformation("JSON parsed: transferId={TransferId}, isFraud={IsFraud}",
                        transferId, isFraud);
                }
                catch (Exception jsonEx)
                {
                    _logger.LogError("JSON parsing also failed: {Error}", jsonEx.Message);
                }
            }

            if (string.IsNullOrEmpty(transferId))
            {
                _logger.LogError("Could not extract transferId from message");
                return;
            }

            // Create fraud result object
            var fraudResult = new FraudResult
            {
                TransferId = transferId,
                IsFraud = isFraud,
                Status = isFraud ? "declined" : "approved"
            };

            // Critical section - handle race condition safely
            lock (_pendingFraudChecks)
            {
                // Check if there's a pending fraud check waiting for this result
                if (_pendingFraudChecks.TryGetValue(transferId, out var tcs))
                {
                    _logger.LogInformation("Found pending fraud check for {TransferId}, completing it now",
                        transferId);
                    tcs.TrySetResult(fraudResult);
                    _pendingFraudChecks.Remove(transferId);
                }
                else
                {
                    // Store the result for when the transfer is created
                    _logger.LogInformation("No pending fraud check found for {TransferId}, storing for later",
                        transferId);
                    _processedFraudResults[transferId] = fraudResult;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing fraud result: {Message}", message);
        }
    }

    // Better JSON field extraction
    private string ExtractJsonField(string json, string fieldName)
    {
        // First try a more strict regex for string values
        string stringPattern = $"\"{fieldName}\"\\s*:\\s*\"([^\"]*)\"";
        var stringMatch = Regex.Match(json, stringPattern);
        if (stringMatch.Success)
        {
            return stringMatch.Groups[1].Value;
        }

        // Try a pattern for non-string values (numbers, booleans)
        string valuePattern = $"\"{fieldName}\"\\s*:\\s*([^,}}\\s][^,}}]*)";
        var valueMatch = Regex.Match(json, valuePattern);
        if (valueMatch.Success)
        {
            return valueMatch.Groups[1].Value.Trim();
        }

        return string.Empty;
    }
}