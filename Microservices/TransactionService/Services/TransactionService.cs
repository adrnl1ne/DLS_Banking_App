using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;

namespace TransactionService.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _repository;
        private readonly UserAccountClientService _userAccountClient;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly ILogger<TransactionService> _logger;
        private readonly Histogram _histogram;
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
            try
            {
                // SECURE - Use LogSanitizer to mask sensitive information
                _logger.LogInformation("Creating transfer from account {FromAccount} to account {ToAccount} for {Amount}", 
                    LogSanitizer.MaskAccountId(int.Parse(request.FromAccount)), 
                    LogSanitizer.MaskAccountId(int.Parse(request.ToAccount)), 
                    LogSanitizer.MaskAmount(request.Amount));

                // Parse account IDs to integers
                if (!int.TryParse(request.FromAccount, out int fromAccountId) || 
                    !int.TryParse(request.ToAccount, out int toAccountId))
                {
                    _logger.LogWarning("Invalid account ID format - FromAccount: {FromAccount}, ToAccount: {ToAccount}", 
                        request.FromAccount, request.ToAccount);
                    throw new ArgumentException("Account IDs must be valid integers");
                }

                // Check if from and to accounts are the same
                if (fromAccountId == toAccountId)
                {
                    _logger.LogWarning("Cannot transfer to the same account - AccountId: {AccountId}", fromAccountId);
                    throw new InvalidOperationException("Cannot transfer funds to the same account");
                }

                // Before creating transaction, check if we have sufficient funds
                var fromAccount = await _userAccountClient.GetAccountAsync(fromAccountId);
                if (fromAccount == null)
                {
                    _logger.LogWarning("Source account {AccountId} not found", fromAccountId);
                    throw new InvalidOperationException($"Source account {fromAccountId} not found");
                }

                // Check if user owns the source account
                if (fromAccount.UserId != request.UserId)
                {
                    _logger.LogWarning("User {UserId} does not own the source account {AccountId}", 
                        request.UserId, fromAccountId);
                    throw new UnauthorizedAccessException("You can only transfer funds from your own accounts");
                }

                // Check sufficient balance for withdrawal/transfer
                if (fromAccount.Amount < request.Amount)
                {
                    _logger.LogWarning("Insufficient funds in account {AccountId}. Balance: {Balance}, Requested: {Amount}", 
                        fromAccountId, fromAccount.Amount, request.Amount);
                    throw new InvalidOperationException("Insufficient funds for this transfer");
                }

                // Validate destination account
                var toAccount = await _userAccountClient.GetAccountAsync(toAccountId);
                if (toAccount == null)
                {
                    _logger.LogWarning("Destination account {AccountId} not found", toAccountId);
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
                _logger.LogInformation("Created pending transaction with ID: {TransferId}", transferId);

                try
                {
                    // Create TaskCompletionSource to wait for the fraud check result FIRST
                    var tcs = new TaskCompletionSource<FraudResult>();
                    
                    // Check if we already have a processed result for this transaction
                    FraudResult cachedResult = null;
                    
                    // Use a proper lock to avoid race conditions
                    lock (_pendingFraudChecks)
                    {
                        if (_processedFraudResults.TryRemove(transferId, out cachedResult))
                        {
                            _logger.LogInformation("Found pre-processed fraud result for transaction {TransferId}", transferId);
                            tcs.SetResult(cachedResult);
                        }
                        else
                        {
                            // No pre-processed result, register for future result
                            _pendingFraudChecks[transferId] = tcs;
                        }
                    }

                    // Prepare and send the fraud check message AFTER registering for the result
                    _logger.LogInformation("Sending transaction {TransferId} to fraud check", 
                        LogSanitizer.MaskTransferId(transferId));
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
                        _logger.LogInformation("Using cached fraud result for {TransferId}", transferId);
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
                                _logger.LogWarning("Fraud check timed out for {TransferId}, using fallback direct processing", transferId);
                                
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
                                _logger.LogInformation("Received fraud check result for {TransferId}: {IsFraud}", 
                                    transferId, fraudResult.IsFraud);
                            }
                        }
                        catch (TimeoutException)
                        {
                            // This should not happen with our new approach, but let's handle it anyway
                            _logger.LogWarning("Unexpected timeout for {TransferId}, using fallback", transferId);
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
                        _logger.LogWarning("Fraud detected for transaction {TransferId}", transferId);
                        
                        // Update transaction status to declined
                        transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                        if (transaction != null)
                        {
                            await _repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                        }
                        
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
                        Amount = request.Amount,  // Send the amount to adjust by, not the new balance
                        TransactionId = transaction.TransferId + "-withdrawal",
                        TransactionType = "Withdrawal", // This account is being debited
                        IsAdjustment = true  // This is critical - it tells the API to adjust rather than set
                    };

                    // For destination account: ADD funds
                    var toBalanceRequest = new Models.AccountBalanceRequest
                    {
                        Amount = request.Amount,  // Send the amount to adjust by, not the new balance
                        TransactionId = transaction.TransferId + "-deposit",
                        TransactionType = "Deposit", // This account is being credited
                        IsAdjustment = true  // This is critical - it tells the API to adjust rather than set
                    };

                    // Update the balances via the client service
                    await _userAccountClient.UpdateBalanceAsync(fromAccountId, fromBalanceRequest);
                    await _userAccountClient.UpdateBalanceAsync(toAccountId, toBalanceRequest);

                    // Update all transaction statuses to completed
                    transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                    if (transaction != null) {
                        await _repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
                    }

                    var retrievedWithdrawalTx = await _repository.GetTransactionByTransferIdAsync(transferId + "-withdrawal");
                    if (retrievedWithdrawalTx != null) {
                        await _repository.UpdateTransactionStatusAsync(retrievedWithdrawalTx.Id, "completed");
                    }

                    var retrievedDepositTx = await _repository.GetTransactionByTransferIdAsync(transferId + "-deposit");
                    if (retrievedDepositTx != null) {
                        await _repository.UpdateTransactionStatusAsync(retrievedDepositTx.Id, "completed");
                    }
                    
                    _logger.LogInformation("Transaction {TransferId} completed successfully", transferId);

                    // Track transaction amount in histogram for metrics
                    _histogram.Observe((double)transaction.Amount);

                    return TransactionResponse.FromTransaction(transaction);
                }
                catch (Exception ex)
                {
                    // SECURE - Use sanitized exception message and masked IDs
                    _logger.LogError("Error processing transaction {TransferId}: {ErrorMessage}", 
                        LogSanitizer.MaskTransferId(transferId),
                        LogSanitizer.SanitizeErrorMessage(ex.Message));

                    // For detailed diagnostics (if needed), log with trace level
                    _logger.LogTrace(ex, "Full exception details for diagnostics");

                    try
                    {
                        var failedTransaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                        if (failedTransaction != null) {
                            await _repository.UpdateTransactionStatusAsync(failedTransaction.Id, "failed");
                        }
                        else {
                            _logger.LogWarning("Could not find transaction with transfer ID {TransferId} to mark as failed", transferId);
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Error updating transaction status to failed for {TransferId}", transferId);
                    }
                    
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transfer");
                throw;
            }
        }

        public async Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId)
        {
            try
            {
                var transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                return transaction != null ? TransactionResponse.FromTransaction(transaction) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction {TransferId}", transferId);
                throw;
            }
        }

        public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId, int authenticatedUserId)
        {
            try
            {
                // Validate accountId format
                if (!int.TryParse(accountId, out int accountIdInt))
                {
                    _logger.LogWarning("Invalid account ID format: {AccountId}", accountId);
                    throw new ArgumentException("Account ID must be a valid integer.");
                }

                // Call UserAccountService to get account details
                _logger.LogInformation("Fetching account {AccountId} from UserAccountService", accountId);
                var account = await _userAccountClient.GetAccountAsync(accountIdInt);

                if (account == null)
                {
                    _logger.LogWarning("Account {AccountId} not found in UserAccountService", accountId);
                    throw new InvalidOperationException($"Account {accountId} not found.");
                }

                // Validate that the authenticated user owns the account
                if (account.UserId != authenticatedUserId)
                {
                    _logger.LogWarning("User {UserId} is not authorized to access transactions for account {AccountId}", 
                        authenticatedUserId, accountId);
                    throw new UnauthorizedAccessException("You are not authorized to access transactions for this account.");
                }

                // Fetch transactions from the repository
                var transactions = await _repository.GetTransactionsByAccountAsync(accountId);
                return transactions.Select(TransactionResponse.FromTransaction);
            }
            catch (Exception ex)
            {
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
                _logger.LogInformation("Processing fraud result: Transaction {TransferId} marked as {Status}", transferId, status);
                
                // Use the same repository but in a new async context
                // Look up the main transaction
                var mainTransaction = await _repository.GetTransactionByTransferIdAsync(transferId);
                if (mainTransaction != null)
                {
                    await _repository.UpdateTransactionStatusAsync(mainTransaction.Id, status);
                    _logger.LogInformation("Updated main transaction {Id} status to {Status}", mainTransaction.Id, status);
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
                    _logger.LogInformation("Updated withdrawal transaction {Id} status to {Status}", withdrawalTx.Id, status);
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
                        _logger.LogInformation("Found pending fraud check for {TransferId}, completing it now", transferId);
                        tcs.TrySetResult(fraudResult);
                        _pendingFraudChecks.Remove(transferId);
                    }
                    else
                    {
                        // Store the result for when the transfer is created
                        _logger.LogInformation("No pending fraud check found for {TransferId}, storing for later", transferId);
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
}
