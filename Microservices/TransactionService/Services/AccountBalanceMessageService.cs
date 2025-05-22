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
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private Timer? _reconnectTimer;
        private const string QUEUE_NAME = "AccountBalanceUpdates";
        private bool _isSubscribed;

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
            _logger.LogInformation("AccountBalanceConsumerService starting");
            
            // Delay initial connection attempt to ensure all services are ready
            _reconnectTimer = new Timer(
                async _ => await EnsureConnectedAsync(), 
                null, 
                TimeSpan.FromSeconds(5), // Initial delay
                TimeSpan.FromSeconds(15)); // Regular interval
            
            return Task.CompletedTask;
        }

        private async Task EnsureConnectedAsync()
        {
            if (!await _connectionSemaphore.WaitAsync(0))
            {
                return; // Another connection attempt is in progress
            }

            try
            {
                if (_isSubscribed && _rabbitMqClient.IsConnected)
                {
                    _logger.LogDebug("Already connected to RabbitMQ and subscribed to {QueueName}", QUEUE_NAME);
                    return;
                }

                _logger.LogInformation("Connecting to RabbitMQ and subscribing to {QueueName}", QUEUE_NAME);
                
                try
                {
                    _rabbitMqClient.EnsureConnection();

                    _rabbitMqClient.Subscribe<AccountBalanceUpdateMessage>(
                        QUEUE_NAME, 
                        async message => await ProcessMessageAsync(message));
                    
                    _isSubscribed = true;
                    _logger.LogInformation("Successfully subscribed to {QueueName}", QUEUE_NAME);
                }
                catch (Exception ex)
                {
                    _isSubscribed = false;
                    _logger.LogError(ex, "Failed to connect to RabbitMQ or subscribe to {QueueName}", QUEUE_NAME);
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task<bool> ProcessMessageAsync(AccountBalanceUpdateMessage message)
        {
            try
            {
                _logger.LogInformation("Processing balance update for account {AccountId}", message.AccountId);
                
                using var scope = _serviceProvider.CreateScope();
                var accountBalanceProcessor = scope.ServiceProvider.GetRequiredService<AccountBalanceProcessingService>();
                
                var success = await accountBalanceProcessor.ProcessBalanceUpdateAsync(message);
                
                if (success)
                {
                    _logger.LogInformation("Successfully processed balance update");
                    return true;
                }
                
                _logger.LogWarning("Failed to process balance update");
                return false; // Retry temporary errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update message");
                return false; // Retry on exception
            }
        }
        
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AccountBalanceConsumerService stopping");
            _reconnectTimer?.Change(Timeout.Infinite, 0);
            _reconnectTimer?.Dispose();
            return base.StopAsync(cancellationToken);
        }
    }
}