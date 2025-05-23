using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Infrastructure.Data.Repositories;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace TransactionService.Services
{
    public class DelayedFraudCheckConsumer : BackgroundService
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DelayedFraudCheckConsumer> _logger;
        private const string QUEUE_NAME = "FraudCheckResults";

        public DelayedFraudCheckConsumer(
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider,
            ILogger<DelayedFraudCheckConsumer> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting DelayedFraudCheckConsumer");
            
            // Implement retry logic for initial queue setup
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    5, // retry 5 times
                    retryAttempt => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, retryAttempt), 30)), // exponential backoff
                    (exception, timeSpan, retryCount, context) => {
                        _logger.LogWarning(exception, 
                            "Failed to setup queue (attempt {RetryCount}), retrying in {RetryInterval}s", 
                            retryCount, timeSpan.TotalSeconds);
                    });

            // Execute with retry policy
            _ = retryPolicy.ExecuteAsync(async () => {
                try
                {
                    // Create the queue
                    _rabbitMqClient.DeclareQueue("FraudCheckResults", durable: true);
                    _logger.LogInformation("FraudCheckResults queue declared successfully");
                    
                    // Subscribe to messages
                    _rabbitMqClient.Subscribe("FraudCheckResults", async (string message) => {
                        _logger.LogInformation("RECEIVED FRAUD RESULT: {Message}", message);
                        
                        try {
                            // First parse the message as a generic JSON object to handle timestamp parsing issues
                            using JsonDocument jsonDocument = JsonDocument.Parse(message);
                            var root = jsonDocument.RootElement;
        
                            // Extract fields manually to avoid timestamp parsing issues
                            var result = new DelayedFraudCheckResult 
                            {
                                EventType = root.GetProperty("event_type").GetString() ?? "unknown",
                                TransferId = root.GetProperty("transferId").GetString() ?? string.Empty,
                                IsFraud = root.TryGetProperty("isFraud", out var fraudElement) && fraudElement.GetBoolean(),
                                Status = root.GetProperty("status").GetString() ?? "unknown",
                                Amount = root.TryGetProperty("amount", out var amountElement) ? amountElement.GetDecimal() : 0,
                                Timestamp = DateTime.UtcNow // Use current time since we can't parse the provided timestamp
                            };
        
                            _logger.LogInformation("Manually parsed fraud check result: {TransferId}, IsFraud={IsFraud}, Status={Status}",
                                result.TransferId, result.IsFraud, result.Status);
        
                            return await ProcessDelayedFraudCheckAsync(result);
                        }
                        catch (Exception ex) {
                            _logger.LogError(ex, "Failed to process fraud check message: {Message}", message);
                            return false;
                        }
                    });
                    
                    _logger.LogInformation("Successfully subscribed to FraudCheckResults queue");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to setup FraudCheckResults queue");
                    throw; // Rethrow to trigger retry
                }
            });
            
            return Task.CompletedTask;
        }

        private async Task<bool> ProcessDelayedFraudCheckAsync(DelayedFraudCheckResult message)
        {
            _logger.LogInformation("Processing delayed fraud check result for transaction");
            
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var rabbitMqClient = scope.ServiceProvider.GetRequiredService<IRabbitMqClient>();
            
            try
            {
                // Get the transaction
                var transaction = await repository.GetTransactionByTransferIdAsync(message.TransferId);
                if (transaction == null)
                {
                    _logger.LogWarning("Transaction not found for delayed fraud check result");
                    return true; // Message processed, just nothing to do
                }
                
                // If the transaction was already declined or completed, ignore
                if (transaction.Status == "declined")
                {
                    _logger.LogInformation("Transaction was already declined, ignoring delayed fraud check");
                    return true;
                }
                
                // Get the from/to accounts from the transaction
                var fromAccountId = transaction.FromAccount;
                var toAccountId = transaction.ToAccount;
                    
                // If fraud was detected in delayed check, mark transaction as declined
                if (message.IsFraud)
                {
                    _logger.LogWarning("Delayed fraud check detected fraud for transaction");
                    transaction.FraudCheckResult = "fraud_detected_after_processing";
                    await repository.UpdateTransactionAsync(transaction);
                    
                    // Set transaction status to declined if fraud was detected
                    await repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                    
                    // Also update child transactions
                    var withdrawalTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-withdrawal");
                    var depositTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-deposit");
                    
                    if (withdrawalTransaction != null)
                        await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "declined");
                        
                    if (depositTransaction != null)
                        await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "declined");
                        
                    _logger.LogInformation("Transaction declined due to delayed fraud detection - no balance updates will be processed");
                    
                    return true;
                }
                else
                {
                    // No fraud detected, update transaction status
                    _logger.LogInformation("No fraud detected for delayed check on transaction");
                    transaction.FraudCheckResult = "verified_after_processing";
                    await repository.UpdateTransactionAsync(transaction);
                    
                    // Update the transaction status if it was still pending
                    if (transaction.Status == "pending" || transaction.Status == "processing")
                    {
                        // Update parent transaction
                        await repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
                        
                        // Also update child transactions if they exist
                        var withdrawalTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-withdrawal");
                        var depositTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-deposit");
                        
                        if (withdrawalTransaction != null)
                            await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "completed");
                            
                        if (depositTransaction != null)
                            await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "completed");
                            
                        // NOW queue balance updates since fraud check passed
                        var fromAccount = int.TryParse(transaction.FromAccount, out var fromId) ? 
                            new Account { Id = fromId, UserId = transaction.UserId } : null;
                        var toAccount = int.TryParse(transaction.ToAccount, out var toId) ? 
                            new Account { Id = toId } : null;
                        
                        if (fromAccount != null && toAccount != null)
                        {
                            // Create messages for RabbitMQ
                            var fromAccountMessage = new AccountBalanceUpdateMessage
                            {
                                AccountId = fromAccount.Id,
                                Amount = transaction.Amount,
                                TransactionId = transaction.TransferId + "-withdrawal",
                                TransactionType = "Withdrawal",
                                IsAdjustment = true,
                                Timestamp = DateTime.UtcNow
                            };
                            
                            var toAccountMessage = new AccountBalanceUpdateMessage
                            {
                                AccountId = toAccount.Id,
                                Amount = transaction.Amount,
                                TransactionId = transaction.TransferId + "-deposit",
                                TransactionType = "Deposit",
                                IsAdjustment = true,
                                Timestamp = DateTime.UtcNow
                            };

                            // Serialize to string before publishing
                            string fromAccountJson = System.Text.Json.JsonSerializer.Serialize(fromAccountMessage);
                            string toAccountJson = System.Text.Json.JsonSerializer.Serialize(toAccountMessage);

                            // Now it's safe to queue the balance updates since we've verified no fraud
                            rabbitMqClient.Publish("AccountBalanceUpdates", fromAccountJson);
                            rabbitMqClient.Publish("AccountBalanceUpdates", toAccountJson);
                            
                            _logger.LogInformation("Balance update messages queued after delayed fraud verification passed");
                        }
                    }
                        
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing delayed fraud check for transaction");
                return false; // Retry
            }
        }
    }
}
