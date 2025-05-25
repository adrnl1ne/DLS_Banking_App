using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    public class FraudResultConsumer : BackgroundService
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FraudResultConsumer> _logger;
        private const string QUEUE_NAME = "FraudResult";
        private Timer? _reconnectTimer;
        private bool _isSubscribed;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        public FraudResultConsumer(
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider,
            ILogger<FraudResultConsumer> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 STARTING FraudResultConsumer - this should appear in logs");
            
            _reconnectTimer = new Timer(
                async _ => await EnsureConnectedAsync(), 
                null, 
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5));
            
            return Task.CompletedTask;
        }

        private async Task EnsureConnectedAsync()
        {
            if (!await _connectionSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (_isSubscribed && _rabbitMqClient.IsConnected)
                {
                    _logger.LogDebug("Already connected to {QueueName}", QUEUE_NAME);
                    return;
                }

                _logger.LogInformation("🔌 CONNECTING to {QueueName} queue", QUEUE_NAME);
                
                _rabbitMqClient.EnsureConnection();
                
                // Declare the queue to make sure it exists - use durable=true to match FraudDetectionService
                try
                {
                    _rabbitMqClient.DeclareQueue(QUEUE_NAME, durable: true);  // Changed to durable=true
                    _logger.LogInformation("✅ Queue {QueueName} declared successfully as durable=true", QUEUE_NAME);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Could not declare queue {QueueName}, will try to subscribe anyway", QUEUE_NAME);
                }
                
                _rabbitMqClient.Subscribe<object>(QUEUE_NAME, async (object message) => {
                    string messageString = message?.ToString() ?? string.Empty;
                    _logger.LogInformation("📨 RECEIVED FRAUD RESULT MESSAGE: {Message}", messageString);
                    return await ProcessFraudResultAsync(messageString);
                });
                
                _isSubscribed = true;
                _logger.LogInformation("✅ SUCCESSFULLY subscribed to {QueueName} - ready to receive fraud results", QUEUE_NAME);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FAILED to connect to {QueueName}", QUEUE_NAME);
                _isSubscribed = false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task<bool> ProcessFraudResultAsync(string message)
        {
            try
            {
                _logger.LogInformation("🔍 PROCESSING fraud result message: {Message}", message);

                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("❌ Empty fraud result message received");
                    return true;
                }

                using JsonDocument jsonDocument = JsonDocument.Parse(message);
                var root = jsonDocument.RootElement;

                var fraudResult = new FraudResult
                {
                    TransferId = root.TryGetProperty("transferId", out var transferIdElement) ? 
                        transferIdElement.GetString() ?? "" : "",
                    IsFraud = root.TryGetProperty("isFraud", out var fraudElement) && fraudElement.GetBoolean(),
                    Status = root.TryGetProperty("status", out var statusElement) ? 
                        statusElement.GetString() ?? "" : "",
                    Amount = root.TryGetProperty("amount", out var amountElement) ? 
                        amountElement.GetDecimal() : 0,
                    Timestamp = DateTime.UtcNow
                };

                if (string.IsNullOrEmpty(fraudResult.TransferId))
                {
                    _logger.LogWarning("❌ Received fraud result without transferId");
                    return true;
                }

                _logger.LogInformation("✅ PARSED fraud result for {TransferId}: IsFraud={IsFraud}, Status={Status}", 
                    fraudResult.TransferId, fraudResult.IsFraud, fraudResult.Status);

                // Process the fraud result through the transaction service
                using var scope = _serviceProvider.CreateScope();
                var transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService>();
                
                _logger.LogInformation("🔄 CALLING ProcessFraudResultAsync for {TransferId}", fraudResult.TransferId);
                await transactionService.ProcessFraudResultAsync(fraudResult);
                
                _logger.LogInformation("✅ SUCCESSFULLY processed fraud result for {TransferId}", fraudResult.TransferId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 ERROR processing fraud result: {Message}", message);
                return false;
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 STOPPING FraudResultConsumer");
            _reconnectTimer?.Change(Timeout.Infinite, 0);
            _reconnectTimer?.Dispose();
            return base.StopAsync(cancellationToken);
        }
    }
}
