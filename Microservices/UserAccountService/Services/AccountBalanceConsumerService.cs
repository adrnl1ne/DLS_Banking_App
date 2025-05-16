using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserAccountService.Infrastructure.Messaging;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;
using UserAccountService.Service; // Only include this namespace

namespace UserAccountService.Services
{
    public class AccountBalanceConsumerService : BackgroundService
    {
        private readonly ILogger<AccountBalanceConsumerService> _logger;
        private readonly IRabbitMqClient _rabbitMqClient;
        private readonly IServiceProvider _serviceProvider;
        private const string QUEUE_NAME = "AccountBalanceUpdates";

        public AccountBalanceConsumerService(
            ILogger<AccountBalanceConsumerService> logger,
            IRabbitMqClient rabbitMqClient,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _rabbitMqClient = rabbitMqClient;
            _serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Account Balance Consumer Service starting");
            
            _rabbitMqClient.Subscribe<AccountBalanceUpdateMessage>(
                QUEUE_NAME, 
                async (message) => await ProcessMessageAsync(message));
            
            return Task.CompletedTask;
        }

        private async Task<bool> ProcessMessageAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation(
                "Processing balance update for account {AccountId}, transaction {TransactionId}",
                message.AccountId, message.TransactionId);
            
            try
            {
                // Create a new scope for dependency resolution
                using var scope = _serviceProvider.CreateScope();
                var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
                
                // Map the message to the request format expected by the AccountService
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.TransactionType,
                    IsAdjustment = message.IsAdjustment
                };
                
                // Call our special method for system-initiated updates
                var result = await accountService.UpdateBalanceAsSystemAsync(message.AccountId, request);
                
                if (result.Success)
                {
                    _logger.LogInformation(
                        "Balance update successful for account {AccountId}, new balance: {Balance}",
                        message.AccountId, result.Data?.Amount);
                    return true;
                }
                
                _logger.LogWarning(
                    "Balance update failed for account {AccountId}: {ErrorMessage}",
                    message.AccountId, result.Message);
                
                // If it's a permanent error, acknowledge the message
                if (result.ErrorCode == "ACCOUNT_NOT_FOUND" || result.ErrorCode == "INVALID_OPERATION")
                {
                    return true;
                }
                
                // Otherwise, requeue for transient errors
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing balance update for account {AccountId}, transaction {TransactionId}",
                    message.AccountId, message.TransactionId);
                
                // Requeue for unexpected errors
                return false;
            }
        }
        
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Account Balance Consumer Service stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}