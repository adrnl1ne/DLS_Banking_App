using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    /// <summary>
    /// A service to process account balance updates by forwarding them to UserAccountService
    /// </summary>
    public class AccountBalanceProcessingService
    {
        private readonly ILogger<AccountBalanceProcessingService> _logger;
        private readonly IUserAccountClient _userAccountClient;
        
        public AccountBalanceProcessingService(
            ILogger<AccountBalanceProcessingService> logger,
            IUserAccountClient userAccountClient)
        {
            _logger = logger;
            _userAccountClient = userAccountClient;
        }

        public async Task<bool> ProcessBalanceUpdateAsync(AccountBalanceUpdateMessage message)
        {
            try
            {
                _logger.LogInformation("Processing balance update for account {AccountId}, amount {Amount}, transaction {TransactionId}", 
                    message.AccountId, message.Amount, message.TransactionId);
                
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.TransactionType,
                    IsAdjustment = message.IsAdjustment
                };
                
                // Forward the balance update to UserAccountService
                await _userAccountClient.UpdateBalanceAsync(message.AccountId, request);
                
                _logger.LogInformation("Successfully forwarded balance update to UserAccountService for account {AccountId}", message.AccountId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update for account {AccountId}, transaction {TransactionId}", 
                    message.AccountId, message.TransactionId);
                return false;
            }
        }
    }
}