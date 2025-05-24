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
        private Timer _reconnectTimer;
        private SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);
        private bool _isSubscribed;

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
            _logger.LogInformation("Starting DelayedFraudCheckConsumer with improved reconnection logic");
            
            // Use a timer to periodically ensure we're connected
            _reconnectTimer = new Timer(
                async _ => await EnsureConnectedAsync(), 
                null, 
                TimeSpan.FromSeconds(2), // Faster initial delay
                TimeSpan.FromSeconds(5)); // Much faster interval - check every 5 seconds like AccountBalance
            
            return Task.CompletedTask;
        }
        
        private async Task EnsureConnectedAsync()
        {
            if (!await _connectionSemaphore.WaitAsync(0))
            {
                _logger.LogTrace("Connection attempt already in progress for {QueueName}.", QUEUE_NAME);
                return;
            }

            try
            {
                if (_isSubscribed && _rabbitMqClient.IsConnected)
                {
                    _logger.LogTrace("Already connected and subscribed to {QueueName}", QUEUE_NAME);
                    return;
                }

                _logger.LogDebug("Attempting to ensure connection and subscription to queue {QueueName}", QUEUE_NAME);

                var retryPolicy = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        3, // Reduce retries to be faster
                        retryAttempt => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, retryAttempt), 10)), // Faster backoff
                        (exception, timeSpan, retryCount, context) => {
                            _logger.LogWarning(exception, 
                                "Failed to setup subscription for {QueueName} (attempt {RetryCount}), retrying in {RetryInterval}s", 
                                QUEUE_NAME, retryCount, timeSpan.TotalSeconds);
                        });

                await retryPolicy.ExecuteAsync(async () => {
                    try
                    {
                        _logger.LogTrace("Ensuring RabbitMQ connection for {QueueName}...", QUEUE_NAME);
                        _rabbitMqClient.EnsureConnection();
                        _logger.LogTrace("RabbitMQ connection established for {QueueName}", QUEUE_NAME);

                        // Use the same subscription pattern as AccountBalanceConsumerService
                        _logger.LogTrace("Attempting to subscribe to queue {QueueName} using generic subscription...", QUEUE_NAME);
                        _rabbitMqClient.Subscribe<object>(QUEUE_NAME, async (object message) => {
                            _logger.LogInformation("RECEIVED DELAYED FRAUD CHECK MESSAGE from {QueueName}: {MessageType}", QUEUE_NAME, message?.GetType().Name);
                            
                            // Convert the message to string for processing
                            string messageString = message?.ToString() ?? string.Empty;
                            _logger.LogInformation("Message content: {MessageContent}", messageString);
                            
                            return await ProcessFraudResultMessageAsync(messageString);
                        });
                        
                        _isSubscribed = true;
                        _logger.LogInformation("Successfully subscribed to {QueueName} using generic subscription pattern.", QUEUE_NAME);
                    }
                    catch (Exception ex)
                    {
                        _isSubscribed = false;
                        _logger.LogError(ex, "Failed to setup RabbitMQ connection/subscription for {QueueName} during attempt.", QUEUE_NAME);
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "All attempts to connect and subscribe to {QueueName} failed.", QUEUE_NAME);
                _isSubscribed = false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task<bool> ProcessFraudResultMessageAsync(string message)
        {
            _logger.LogInformation("Processing fraud result message: {Length} bytes", message?.Length ?? 0);
            
            if (string.IsNullOrEmpty(message))
            {
                _logger.LogWarning("Received empty message, acknowledging");
                return true;
            }
            
            try {
                // First parse the message as a generic JSON object to handle timestamp parsing issues
                using JsonDocument jsonDocument = JsonDocument.Parse(message);
                var root = jsonDocument.RootElement;

                _logger.LogInformation("Parsed JSON message with {NumProperties} properties", 
                    root.EnumerateObject().Count());

                // Log all properties to help with debugging
                foreach (var prop in root.EnumerateObject())
                {
                    _logger.LogDebug("Message property {Name}: {Value}", 
                        prop.Name, prop.Value.ToString());
                }

                // Check if this is a delayed fraud check result by looking for event_type
                string eventType = root.TryGetProperty("event_type", out var eventTypeElement) ? 
                    eventTypeElement.GetString() ?? "" : "";
                
                if (eventType != "DelayedFraudCheckCompleted")
                {
                    _logger.LogInformation("Skipping message - not a DelayedFraudCheckCompleted event, event_type: {EventType}", eventType);
                    return true; // Acknowledge but don't process
                }

                // Extract fields manually to avoid timestamp parsing issues
                var result = new DelayedFraudCheckResult 
                {
                    EventType = eventType,
                    
                    // Handle both camelCase and snake_case field names for compatibility
                    TransferId = root.TryGetProperty("transferId", out var transferIdElement) ? 
                        transferIdElement.GetString() ?? "" : 
                        root.TryGetProperty("transfer_id", out var transferIdSnake) ? 
                            transferIdSnake.GetString() ?? "" : "",
                    
                    IsFraud = root.TryGetProperty("isFraud", out var fraudElement) ? 
                        fraudElement.GetBoolean() : 
                        root.TryGetProperty("is_fraud", out var fraudSnake) && fraudSnake.GetBoolean(),
                    
                    Status = root.TryGetProperty("status", out var statusElement) ? 
                        statusElement.GetString() ?? "" : "",
                    
                    Amount = root.TryGetProperty("amount", out var amountElement) ? 
                        amountElement.GetDecimal() : 0,
                };

                if (string.IsNullOrEmpty(result.TransferId))
                {
                    _logger.LogWarning("Message missing transferId, acknowledging but not processing: {Message}", message);
                    return true;
                }

                // Log the raw message to help debugging
                _logger.LogInformation("Processing delayed fraud check result: TransferId={TransferId}, IsFraud={IsFraud}, Status={Status}, Amount={Amount}", 
                    result.TransferId, result.IsFraud, result.Status, result.Amount);

                // Attempt to parse the timestamp from the message
                if (root.TryGetProperty("timestamp", out var timestampElement) && timestampElement.ValueKind == JsonValueKind.String)
                {
                    string timestampStr = timestampElement.GetString() ?? "";
                    DateTime parsedTimestamp;
                    
                    // Try parsing with microseconds, then milliseconds, then general ISO 8601
                    if (DateTime.TryParseExact(timestampStr, "yyyy-MM-ddTHH:mm:ss.ffffffZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out parsedTimestamp) ||
                        DateTime.TryParseExact(timestampStr, "yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out parsedTimestamp) ||
                        DateTime.TryParse(timestampStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out parsedTimestamp))
                    {
                        result.Timestamp = parsedTimestamp;
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse timestamp '{TimestampStr}' from fraud check result, using DateTime.UtcNow.", timestampStr);
                        result.Timestamp = DateTime.UtcNow; // Fallback
                    }
                }
                else
                {
                    _logger.LogWarning("Timestamp not found or not a string in fraud check result, using DateTime.UtcNow.");
                    result.Timestamp = DateTime.UtcNow; // Fallback if timestamp property is missing or not a string
                }

                _logger.LogInformation("Successfully parsed delayed fraud check result for {TransferId}: EventType={EventType}, IsFraud={IsFraud}, Status={Status}, Timestamp={Timestamp}",
                    result.TransferId, result.EventType, result.IsFraud, result.Status, result.Timestamp);

                return await ProcessDelayedFraudCheckAsync(result);
            }
            catch (JsonException ex) {
                _logger.LogError(ex, "Failed to parse JSON message: {Message}", message);
                return true; // Acknowledge malformed JSON to avoid infinite retry
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failed to process fraud check message: {Message}", message);
                return false; // Retry processing errors
            }
        }

        private async Task<bool> ProcessDelayedFraudCheckAsync(DelayedFraudCheckResult message)
        {
            _logger.LogInformation("Processing delayed fraud check result for transaction {TransferId}, IsFraud={IsFraud}, Amount={Amount}", 
                message.TransferId, message.IsFraud, message.Amount);
            
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var rabbitMqClient = scope.ServiceProvider.GetRequiredService<IRabbitMqClient>();
            
            try
            {
                var transaction = await repository.GetTransactionByTransferIdAsync(message.TransferId);
                if (transaction == null)
                {
                    _logger.LogWarning("Transaction not found for delayed fraud check result: {TransferId}. Acknowledging message.", message.TransferId);
                    return true; 
                }
                
                _logger.LogInformation("Found transaction {TransferId} for delayed fraud check: Current Status={Status}", 
                    transaction.TransferId, transaction.Status);
                
                if (transaction.Status == "declined" && message.IsFraud)
                {
                    _logger.LogInformation("Transaction {TransferId} was already declined and fraud is confirmed by delayed check. No action needed. Acknowledging message.", 
                        transaction.TransferId);
                    return true;
                }
                if (transaction.Status == "completed" && !message.IsFraud)
                {
                     _logger.LogInformation("Transaction {TransferId} was already completed and no fraud is confirmed by delayed check. No action needed. Acknowledging message.", 
                        transaction.TransferId);
                    return true;
                }

                if (message.IsFraud)
                {
                    _logger.LogWarning("Delayed fraud check detected fraud for transaction {TransferId}", transaction.TransferId);
                    transaction.FraudCheckResult = "fraud_detected_after_processing";
                    await repository.UpdateTransactionAsync(transaction);
                    
                    if (transaction.Status != "declined")
                    {
                        await repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                        var withdrawalTx = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-withdrawal");
                        if (withdrawalTx != null) await repository.UpdateTransactionStatusAsync(withdrawalTx.Id, "declined");
                        var depositTx = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-deposit");
                        if (depositTx != null) await repository.UpdateTransactionStatusAsync(depositTx.Id, "declined");
                        _logger.LogInformation("Transaction {TransferId} and its children marked as declined due to delayed fraud detection.", transaction.TransferId);
                    }
                    _logger.LogInformation("Delayed fraud detected for {TransferId}. Transaction declined. Acknowledging message.", message.TransferId);
                    return true;
                }
                else // No fraud detected by the delayed check
                {
                    _logger.LogInformation("No fraud detected by delayed check for transaction {TransferId}.", transaction.TransferId);
                    transaction.FraudCheckResult = "verified_after_processing";
                    await repository.UpdateTransactionAsync(transaction);
                    
                    if (transaction.Status == "pending" || transaction.Status == "processing")
                    {
                        _logger.LogInformation("Updating transaction {TransferId} status from {CurrentStatus} to completed based on delayed fraud check.", 
                            transaction.TransferId, transaction.Status);
                        
                        await repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
                        
                        var withdrawalTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-withdrawal");
                        if (withdrawalTransaction != null)
                        {
                            await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "completed");
                        }
                            
                        var depositTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-deposit");
                        if (depositTransaction != null)
                        {
                            await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "completed");
                        }
                            
                        var fromAccount = int.TryParse(transaction.FromAccount, out var fromId) ? 
                            new Account { Id = fromId, UserId = transaction.UserId } : null;
                        var toAccount = int.TryParse(transaction.ToAccount, out var toId) ? 
                            new Account { Id = toId } : null;
                        
                        if (fromAccount != null && toAccount != null)
                        {
                            _logger.LogInformation("Queueing balance updates for transaction {TransferId} after fraud verification passed.", 
                                transaction.TransferId);
                                
                            var fromAccountMessage = new AccountBalanceUpdateMessage
                            {
                                AccountId = fromAccount.Id,
                                Amount = transaction.Amount,
                                TransactionId = transaction.TransferId + "-withdrawal",
                                TransactionType = "Withdrawal",
                                IsAdjustment = true, // This indicates it's a result of a completed transaction
                                Timestamp = DateTime.UtcNow
                            };
                            
                            var toAccountMessage = new AccountBalanceUpdateMessage
                            {
                                AccountId = toAccount.Id,
                                Amount = transaction.Amount,
                                TransactionId = transaction.TransferId + "-deposit",
                                TransactionType = "Deposit",
                                IsAdjustment = true, // This indicates it's a result of a completed transaction
                                Timestamp = DateTime.UtcNow
                            };

                            try
                            {
                                string fromAccountJson = System.Text.Json.JsonSerializer.Serialize(fromAccountMessage);
                                string toAccountJson = System.Text.Json.JsonSerializer.Serialize(toAccountMessage);
    
                                _logger.LogDebug("Ensuring connection and declaring queue 'AccountBalanceUpdates' for {TransferId}", transaction.TransferId);
                                rabbitMqClient.EnsureConnection(); 
                                rabbitMqClient.DeclareQueue("AccountBalanceUpdates", durable: true);
                                _logger.LogInformation("Queue 'AccountBalanceUpdates' declared for {TransferId}", transaction.TransferId);
    
                                rabbitMqClient.Publish("AccountBalanceUpdates", fromAccountJson);
                                _logger.LogInformation("Published withdrawal balance update for account {AccountId}, transaction {TransferId}", fromAccount.Id, transaction.TransferId);
                                
                                rabbitMqClient.Publish("AccountBalanceUpdates", toAccountJson);
                                _logger.LogInformation("Published deposit balance update for account {AccountId}, transaction {TransferId}", toAccount.Id, transaction.TransferId);
                                
                                _logger.LogInformation("Balance update messages queued for {TransferId} after delayed fraud verification.", transaction.TransferId);
                            }
                            catch (Exception ex_publish)
                            {
                                _logger.LogError(ex_publish, "Failed to queue balance updates for transaction {TransferId} after successful delayed fraud check. Transaction status updated to completed, but balance update requires reconciliation.", transaction.TransferId);
                                // IMPORTANT: Do not return false here. The fraud check was processed and transaction status updated.
                                // A failure to queue balance updates is a separate issue.
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not parse account IDs for transaction {TransferId} (From: {FromAccount}, To: {ToAccount}). Cannot queue balance updates.", 
                                transaction.TransferId, transaction.FromAccount, transaction.ToAccount);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Transaction {TransferId} status is '{Status}', not 'pending' or 'processing'. No status update or balance queueing needed from this delayed check.", 
                            transaction.TransferId, transaction.Status);
                    }
                    _logger.LogInformation("Delayed fraud check processed for {TransferId}, no fraud found. Acknowledging message.", message.TransferId);
                    return true; 
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in ProcessDelayedFraudCheckAsync for transaction {TransferId}. Message will be NACK'd for retry.", message.TransferId);
                return false; // NACK: retry critical processing error
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping DelayedFraudCheckConsumer");
            _reconnectTimer?.Change(Timeout.Infinite, 0);
            _reconnectTimer?.Dispose();
            return base.StopAsync(cancellationToken);
        }
    }
}
