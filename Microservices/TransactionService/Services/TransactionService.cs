using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
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
            
            // Subscribe to fraud detection results - now with fault tolerance
            try
            {
                _rabbitMqClient.Subscribe("TransactionServiceQueue", HandleFraudResult);
            }
            catch (Exception ex)
            {
                // Log but don't crash - we'll operate in degraded mode if RabbitMQ is unavailable
                _logger.LogError(ex, "Failed to subscribe to TransactionServiceQueue. Fraud detection will be skipped.");
            }
        }

        public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
        {
            try
            {
                _logger.LogInformation("Creating transfer from {FromAccount} to {ToAccount} for {Amount}", 
                    request.FromAccount, request.ToAccount, request.Amount);

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

                // Validate source account
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
                    TransactionType = request.TransactionType, // Use the specified transaction type
                    Description = request.Description ?? $"Transfer from account {fromAccountId} to {toAccountId}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Save the pending transaction
                await _repository.CreateTransactionAsync(transaction);
                _logger.LogInformation("Created pending transaction with ID: {TransferId}", transferId);

                try
                {
                    // Try to send to fraud check 
                    var fraudMessage = new
                    {
                        transferId = transaction.TransferId,
                        fromAccount = transaction.FromAccount,
                        toAccount = transaction.ToAccount,
                        amount = transaction.Amount,
                        userId = transaction.UserId,
                        timestamp = DateTime.UtcNow
                    };

                    _logger.LogInformation("Sending transaction {TransferId} to fraud check", transferId);
                    _rabbitMqClient.Publish("CheckFraud", JsonSerializer.Serialize(fraudMessage));

                    // Create withdrawal transaction for source account
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
                    
                    // Update account balances
                    _logger.LogInformation("Updating balances for transaction {TransferId}", transferId);
                    await _userAccountClient.UpdateBalanceAsync(fromAccountId, fromAccount.Amount - transaction.Amount);
                    await _userAccountClient.UpdateBalanceAsync(toAccountId, toAccount.Amount + transaction.Amount);

                    // Complete all transactions
                    transaction.Status = "completed";
                    withdrawalTransaction.Status = "completed";
                    depositTransaction.Status = "completed";
                    
                    await _repository.UpdateTransactionStatusAsync(transferId, "completed");
                    await _repository.UpdateTransactionStatusAsync(transferId + "-withdrawal", "completed");
                    await _repository.UpdateTransactionStatusAsync(transferId + "-deposit", "completed");
                    
                    _logger.LogInformation("Transaction {TransferId} completed successfully", transferId);

                    // Track transaction amount in histogram
                    _histogram.Observe((double)transaction.Amount);

                    return TransactionResponse.FromTransaction(transaction);
                }
                catch (Exception ex)
                {
                    // If anything fails after creating the transaction, update status to failed
                    _logger.LogError(ex, "Error processing transaction {TransferId}", transferId);
                    
                    try
                    {
                        await _repository.UpdateTransactionStatusAsync(transferId, "failed");
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
                    _logger.LogWarning("Received fraud result for unknown transfer: {TransferId}", transferId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing fraud result for transfer {TransferId}", transferId);
            }
        }

        // Handle fraud check results from RabbitMQ
        private void HandleFraudResult(string message)
        {
            try
            {
                var result = JsonSerializer.Deserialize<FraudResult>(message);
                if (result == null || string.IsNullOrEmpty(result.TransferId))
                {
                    _logger.LogWarning("Received invalid fraud result message: {Message}", message);
                    return;
                }

                _logger.LogInformation("Received fraud result for {TransferId}: {IsFraud}", result.TransferId, result.IsFraud);
                ProcessFraudResult(result.TransferId, result.IsFraud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing fraud result message: {Message}", message);
            }
        }
    }
}
