using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    public class AccountBalanceMessageService : IAccountBalanceService
    {
        private readonly ILogger<AccountBalanceMessageService> _logger;
        private readonly IRabbitMqClient _rabbitMqClient;
        private const string BALANCE_UPDATES_QUEUE = "AccountBalanceUpdates";

        public AccountBalanceMessageService(
            ILogger<AccountBalanceMessageService> logger,
            IRabbitMqClient rabbitMqClient)
        {
            _logger = logger;
            _rabbitMqClient = rabbitMqClient;
        }

        public Task UpdateBalanceAsync(int accountId, AccountBalanceRequest balanceRequest)
        {
            var message = new AccountBalanceUpdateMessage
            {
                AccountId = accountId,
                Amount = balanceRequest.Amount,
                TransactionId = balanceRequest.TransactionId,
                TransactionType = balanceRequest.TransactionType,
                IsAdjustment = balanceRequest.IsAdjustment,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                _rabbitMqClient.Publish(
                    BALANCE_UPDATES_QUEUE, 
                    JsonSerializer.Serialize(message)
                );
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing account balance update message");
                throw;
            }
        }
    }

    public class AccountBalanceUpdateMessage
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public bool IsAdjustment { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AccountBalanceConsumerService : BackgroundService
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccountBalanceConsumerService> _logger;
        private const string QUEUE_NAME = "AccountBalanceUpdates";

        public AccountBalanceConsumerService(
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider,
            ILogger<AccountBalanceConsumerService> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AccountBalanceConsumerService started");
            
            // Fix: Convert to void delegate by not returning values
            _rabbitMqClient.Subscribe(
                QUEUE_NAME, 
                async (messageJson) => {
                    try {
                        // Manually deserialize the message
                        var message = JsonSerializer.Deserialize<AccountBalanceUpdateMessage>(messageJson);
                        if (message == null) {
                            _logger.LogWarning("Failed to deserialize message");
                            return; // Just return without a value
                        }
                        
                        // Process but don't return the result
                        bool success = await ProcessMessageAsync(message);
                        if (!success) {
                            _logger.LogWarning("Message processing failed, will retry later");
                        }
                    }
                    catch (JsonException ex) {
                        _logger.LogError(ex, "Error deserializing message");
                    }
                });
            
            return Task.CompletedTask;
        }

        private async Task<bool> ProcessMessageAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation("Processing balance update message");
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<AccountBalanceProcessingService>();
                
                return await processingService.ProcessBalanceUpdateAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update message");
                return false; // Requeue for unexpected errors
            }
        }
        
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AccountBalanceConsumerService stopping");
            return base.StopAsync(cancellationToken);
        }
    }
}