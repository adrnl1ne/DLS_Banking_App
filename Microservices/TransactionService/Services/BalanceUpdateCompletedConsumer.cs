using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using Polly;

namespace TransactionService.Services
{
    public class BalanceUpdateCompletedConsumer : BackgroundService
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BalanceUpdateCompletedConsumer> _logger;
        private const string QUEUE_NAME = "BalanceUpdateCompleted";

        public BalanceUpdateCompletedConsumer(
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider,
            ILogger<BalanceUpdateCompletedConsumer> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting BalanceUpdateCompletedConsumer");

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
                    _rabbitMqClient.DeclareQueue(QUEUE_NAME, durable: true);
                    _logger.LogInformation("BalanceUpdateCompleted queue declared successfully");
                    
                    // Subscribe to the queue
                    _rabbitMqClient.Subscribe<object>(QUEUE_NAME, message =>
                    {
                        _logger.LogInformation("Received message on BalanceUpdateCompleted: {Message}", message);
                        return ProcessBalanceUpdateCompletedAsync(message);
                    });
                    
                    _logger.LogInformation("Successfully subscribed to BalanceUpdateCompleted queue");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to setup queue for balance update completed events");
                    throw; // Rethrow to trigger retry
                }
            });
            
            return Task.CompletedTask;
        }

        private async Task<bool> ProcessBalanceUpdateCompletedAsync(object messageJson)
        {
            _logger.LogInformation("Processing balance update completed event: {Message}", messageJson);
            
            try
            {
                // Parse message
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var message = JsonSerializer.Deserialize<BalanceUpdateCompletedEvent>((string)messageJson, options);
                
                if (message == null)
                {
                    _logger.LogWarning("Failed to deserialize message: {Message}", messageJson);
                    return true; // Acknowledge the message to avoid reprocessing
                }
                
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
                
                // Extract the base transfer ID from the transaction ID
                string baseTransferId = message.TransactionId;
                if (baseTransferId.EndsWith("-withdrawal") || baseTransferId.EndsWith("-deposit"))
                {
                    baseTransferId = baseTransferId.Substring(0, baseTransferId.LastIndexOf('-'));
                }
                
                _logger.LogInformation("Resolved base transaction ID: {BaseTransferId}", baseTransferId);
                
                // Get the parent transaction
                var transaction = await repository.GetTransactionByTransferIdAsync(baseTransferId);
                if (transaction == null)
                {
                    _logger.LogWarning("Parent transaction not found for balance update: {TransferId}", baseTransferId);
                    return true; // Message processed, just nothing to do
                }
                
                // Only update status if it's not already completed or declined
                if (transaction.Status != "completed" && transaction.Status != "declined")
                {
                    _logger.LogInformation("Updating transaction {TransferId} status to 'completed'", baseTransferId);
                    
                    // Update transaction status to completed
                    await repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
                    
                    // Also update the specific child transaction
                    var childTransaction = await repository.GetTransactionByTransferIdAsync(message.TransactionId);
                    if (childTransaction != null)
                    {
                        await repository.UpdateTransactionStatusAsync(childTransaction.Id, "completed");
                    }
                    
                    _logger.LogInformation("Updated transaction status to 'completed' after balance update confirmation");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update completed event");
                return false; // Retry
            }
        }
    }

    public class BalanceUpdateCompletedEvent
    {
        public string TransactionId { get; set; } = string.Empty;
        public int AccountId { get; set; }
        public decimal NewBalance { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
    }
}
