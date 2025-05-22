using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    /// <summary>
    /// A service to process account balance updates using the UserAccountClient
    /// </summary>
    public class AccountBalanceProcessingService
    {
        private readonly IUserAccountClient _userAccountClient;
        private readonly ILogger<AccountBalanceProcessingService> _logger;

        public AccountBalanceProcessingService(
            IUserAccountClient userAccountClient,
            ILogger<AccountBalanceProcessingService> logger)
        {
            _userAccountClient = userAccountClient;
            _logger = logger;
        }

        public async Task<bool> ProcessBalanceUpdateAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation("Processing balance update request");
                       
            try
            {
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.TransactionType,
                    IsAdjustment = message.IsAdjustment
                };
                
                await _userAccountClient.UpdateBalanceAsync(message.AccountId, request);
                _logger.LogInformation("Balance update successful");
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("404") || ex.Message.Contains("NotFound"))
            {
                _logger.LogWarning("Account not found - won't retry");
                return true;  // Acknowledge permanent errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update");
                return false;  // Retry on exception
            }
        }
    }
}