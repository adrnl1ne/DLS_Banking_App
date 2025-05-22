using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserAccountService.Infrastructure.Messaging;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service
{
    public class AccountBalanceConsumerService : BackgroundService
    {
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccountBalanceConsumerService> _logger;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private Timer? _reconnectTimer;
        private const string QUEUE_NAME = "AccountBalanceUpdates";
        private bool _isConnected;

        public AccountBalanceConsumerService(
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider,
            ILogger<AccountBalanceConsumerService> logger)
        {
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AccountBalanceConsumerService starting");
            
            // Ensure the queue is created immediately at startup
            try
            {
                _rabbitMqClient.EnsureQueueExists("AccountBalanceUpdates", true);
                _logger.LogInformation("AccountBalanceUpdates queue created or confirmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create AccountBalanceUpdates queue");
                // Don't throw here, we'll retry later
            }
            
            // Continue with subscription in the background
            _reconnectTimer = new Timer(
                async _ => await EnsureConnectedAsync(), 
                null, 
                TimeSpan.FromSeconds(5), 
                TimeSpan.FromSeconds(5)); // Retry more frequently
            
            return Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // No longer needed, connection logic is in StartAsync
            return Task.CompletedTask;
        }

        private async Task EnsureConnectedAsync()
        {
            if (_isConnected && _rabbitMqClient.IsConnected)
            {
                return; // Already connected and subscribed
            }

            // Prevent multiple concurrent connection attempts
            if (!await _connectionSemaphore.WaitAsync(0))
            {
                return; // Another connection attempt is already in progress
            }

            try
            {
                _logger.LogInformation("Attempting to connect to RabbitMQ and subscribe to {QueueName}", QUEUE_NAME);
                
                try
                {
                    // Ensure we have a valid connection
                    _rabbitMqClient.EnsureConnection();

                    // Subscribe to the queue with a handler that processes messages
                    _rabbitMqClient.Subscribe<AccountBalanceUpdateMessage>(
                        QUEUE_NAME, 
                        async message => await ProcessMessageAsync(message));
                    
                    _isConnected = true;
                    _logger.LogInformation("Successfully connected to RabbitMQ and subscribed to {QueueName}", QUEUE_NAME);
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    _logger.LogError(ex, "Failed to connect to RabbitMQ or subscribe to {QueueName}", QUEUE_NAME);
                    // We'll retry on the next timer tick
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task<bool> ProcessMessageAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation("Processing balance update");
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
                
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.TransactionType,
                    IsAdjustment = message.IsAdjustment
                };
                
                var result = await accountService.UpdateBalanceAsSystemAsync(message.AccountId, request);
                
                if (result.Success)
                {
                    _logger.LogInformation("Successfully processed balance update");
                    return true; // Message processed successfully
                }
                
                if (result.ErrorCode == "ACCOUNT_NOT_FOUND" || result.ErrorCode == "INVALID_OPERATION")
                {
                    _logger.LogWarning("Permanent error processing message: {ErrorCode}", result.ErrorCode);
                    return true; // Don't requeue for permanent errors
                }
                
                _logger.LogWarning("Temporary error processing message: {ErrorMessage}", result.Message);
                return false; // Requeue for temporary errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update");
                return false; // Requeue on exception
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