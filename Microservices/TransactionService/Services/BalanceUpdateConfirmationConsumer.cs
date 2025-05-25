using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    public class BalanceUpdateConfirmationConsumer : BackgroundService
    {
        private readonly ILogger<BalanceUpdateConfirmationConsumer> _logger;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;

        public BalanceUpdateConfirmationConsumer(
            ILogger<BalanceUpdateConfirmationConsumer> logger,
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔄 STARTING BalanceUpdateConfirmationConsumer");

            try
            {
                // Ensure the queue exists
                _rabbitMqClient.DeclareQueue("BalanceUpdateConfirmation", durable: true);

                // Subscribe to balance update confirmations with proper message handling
                _rabbitMqClient.Subscribe<object>("BalanceUpdateConfirmation", async (messageObj) =>
                {
                    try
                    {
                        // Convert the message object to JSON string first
                        string messageJson = messageObj?.ToString() ?? "";
                        
                        if (string.IsNullOrEmpty(messageJson))
                        {
                            _logger.LogWarning("⚠️ Received empty message");
                            return true; // Acknowledge empty messages
                        }

                        _logger.LogInformation("📨 RAW MESSAGE RECEIVED: {Message}", messageJson);

                        // Parse the JSON manually to handle property name casing
                        using var document = JsonDocument.Parse(messageJson);
                        var root = document.RootElement;

                        // Extract fields with proper null checks
                        var transferId = root.TryGetProperty("transferId", out var transferIdProp) 
                            ? transferIdProp.GetString() ?? "" 
                            : "";

                        var transactionId = root.TryGetProperty("transactionId", out var transactionIdProp) 
                            ? transactionIdProp.GetString() ?? "" 
                            : "";

                        var transactionType = root.TryGetProperty("transactionType", out var transactionTypeProp) 
                            ? transactionTypeProp.GetString() ?? "" 
                            : "";

                        var success = root.TryGetProperty("success", out var successProp) 
                            ? successProp.GetBoolean() 
                            : false;

                        // Log the parsed values for debugging
                        _logger.LogInformation("📋 PARSED VALUES: TransferId={TransferId}, TransactionId={TransactionId}, Type={Type}, Success={Success}", 
                            transferId, transactionId, transactionType, success);

                        if (string.IsNullOrEmpty(transferId))
                        {
                            _logger.LogWarning("⚠️ Missing transferId in message, acknowledging anyway");
                            return true;
                        }

                        _logger.LogInformation("📨 RECEIVED balance update confirmation: TransferId={TransferId}, Type={Type}, Success={Success}", 
                            transferId, transactionType, success);

                        using var scope = _serviceProvider.CreateScope();
                        var transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService>();

                        // Handle the balance update confirmation
                        await transactionService.HandleBalanceUpdateConfirmationAsync(transferId, transactionType, success);

                        _logger.LogInformation("✅ PROCESSED balance update confirmation for {TransferId}", transferId);
                        return true;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "❌ JSON parsing error in balance update confirmation");
                        return true; // Acknowledge malformed JSON to avoid infinite retry
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ ERROR processing balance update confirmation");
                        return false; // Will be requeued
                    }
                });

                _logger.LogInformation("✅ BalanceUpdateConfirmationConsumer subscribed successfully");

                // Keep the service running
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 FATAL ERROR in BalanceUpdateConfirmationConsumer");
                throw;
            }
        }
    }

    public class BalanceUpdateConfirmationMessage
    {
        public string TransferId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Timestamp { get; set; } = string.Empty;
    }
}
