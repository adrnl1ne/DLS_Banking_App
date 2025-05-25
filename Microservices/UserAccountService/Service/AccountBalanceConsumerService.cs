using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserAccountService.Infrastructure.Messaging;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;
using UserAccountService.Service;

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
            
            // Force reconnection by setting _isConnected to false
            _isConnected = false;
            
            // Start with immediate connection attempt, then use longer intervals
            _reconnectTimer = new Timer(
                async _ => await InitializeQueueAndSubscribe(), 
                null, 
                TimeSpan.FromSeconds(2), // Short initial delay 
                TimeSpan.FromSeconds(30)); // Longer interval - only check every 30 seconds
    
            return base.StartAsync(cancellationToken);
        }

        private async Task InitializeQueueAndSubscribe()
        {
            // Only try to reconnect if not already connected
            if (_isConnected && _rabbitMqClient.IsConnected)
            {
                _logger.LogTrace("Already connected and subscribed to {QueueName}", QUEUE_NAME);
                return;
            }

            // Try to initialize the queue first
            try
            {
                _logger.LogInformation("Initializing queue {QueueName}", QUEUE_NAME);
                
                // Make sure the queue exists before subscribing
                await Task.Run(() => {
                    try 
                    {
                        _rabbitMqClient.EnsureQueueExists(QUEUE_NAME, true);
                        _logger.LogInformation("Queue {QueueName} created or confirmed", QUEUE_NAME);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error ensuring queue {QueueName} exists", QUEUE_NAME);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize queue {QueueName}", QUEUE_NAME);
            }
            
            // Then try to connect and subscribe
            await EnsureConnectedAsync();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // The main work happens in StartAsync and the timer callbacks
            return Task.CompletedTask;
        }

        private async Task EnsureConnectedAsync()
        {
            // Skip if already connected
            if (_isConnected && _rabbitMqClient.IsConnected)
            {
                _logger.LogDebug("Already connected to RabbitMQ and subscribed to {QueueName}", QUEUE_NAME);
                return;
            }

            // Try to acquire the semaphore, but don't block if already being attempted
            if (!await _connectionSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                _logger.LogInformation("Attempting to connect to RabbitMQ and subscribe to {QueueName}", QUEUE_NAME);
                
                try
                {
                    // Make sure we have a fresh connection
                    _rabbitMqClient.EnsureConnection();
                    _logger.LogInformation("RabbitMQ connection established");

                    // Subscribe to the queue with our handler
                    _rabbitMqClient.SubscribeAsync<AccountBalanceUpdateMessage>(
                        QUEUE_NAME, 
                        async message =>
                        {
                            _logger.LogInformation("Received message");
                            return await ProcessMessageAsync(message);
                        });
                
                    _isConnected = true;
                    _logger.LogInformation("Successfully connected to RabbitMQ and subscribed to {QueueName}", QUEUE_NAME);
                }
                catch (Exception ex)
                {
                    _isConnected = false;
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
                _logger.LogInformation("Processing balance update");
                
                // Create the request object based on the message
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.IsAdjustment ? 
                        message.TransactionType : 
                        (!string.IsNullOrEmpty(message.TransactionType) ? message.TransactionType : "Deposit"),
                    IsAdjustment = message.IsAdjustment
                };
                
                _logger.LogInformation("Calling UpdateBalanceAsSystemAsync");

                // Get account service from DI
                using var scope = _serviceProvider.CreateScope();
                var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
                
                // Process via system update method
                var result = await accountService.UpdateBalanceAsSystemAsync(message.AccountId, request);
                
                if (result.Success)
                {
                    _logger.LogInformation("Successfully processed balance update");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Permanent error processing message: {ErrorCode} - {Message}", result.ErrorCode, result.Message);
                    return true; // Don't requeue for permanent errors
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing account balance update: {Message}", ex.Message);
                return false;
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