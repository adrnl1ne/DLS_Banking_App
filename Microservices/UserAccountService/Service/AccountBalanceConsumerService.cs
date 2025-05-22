using System;
using System.Text.Json;
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
        private const string QUEUE_NAME = "AccountBalanceUpdates";
        private readonly ILogger<AccountBalanceConsumerService> _logger;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private Timer _reconnectTimer;

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
            
            // Start a timer to periodically check and ensure we're connected
            _reconnectTimer = new Timer(async _ => await EnsureConnectedAsync(),
                null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            
            return Task.CompletedTask;
        }

        private async Task EnsureConnectedAsync()
        {
            // Use semaphore to prevent multiple concurrent connection attempts
            if (!await _connectionSemaphore.WaitAsync(0))
            {
                return; // Another connection attempt is in progress
            }

            try
            {
                _logger.LogDebug("Checking RabbitMQ connection and consumer status");
                
                try
                {
                    // Use the generic Subscribe<T> method with explicit type parameter
                    _rabbitMqClient.Subscribe<AccountBalanceUpdateMessage>(
                        QUEUE_NAME, 
                        async (message) => {
                            try
                            {
                                await ProcessMessageAsync(message);
                                return true; // Successfully processed
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing message asynchronously");
                                return false; // Failed to process, requeue
                            }
                        });
                
                    _logger.LogInformation("Successfully subscribed to {QueueName}", QUEUE_NAME);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe to RabbitMQ queue {QueueName}", QUEUE_NAME);
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task<bool> ProcessMessageAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation("Processing balance update for account ID: {AccountId}", message.AccountId);
            
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
                    _logger.LogInformation("Successfully processed balance update for account: {AccountId}", 
                        message.AccountId);
                    return true; // Success
                }
                else
                {
                    if (result.ErrorCode == "ACCOUNT_NOT_FOUND" || result.ErrorCode == "INVALID_OPERATION")
                    {
                        _logger.LogWarning("Permanent error processing message: {ErrorCode}, {ErrorMessage}", 
                            result.ErrorCode, result.Message);
                        return true; // Acknowledge permanent errors
                    }
                    else
                    {
                        _logger.LogWarning("Temporary error processing message: {ErrorMessage}", result.Message);
                        return false; // Requeue for temporary errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update for account: {AccountId}", message.AccountId);
                return false; // Requeue on exceptions
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