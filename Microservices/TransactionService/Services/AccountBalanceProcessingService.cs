using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace TransactionService.Services
{
    /// <summary>
    /// A service to process account balance updates using the UserAccountClient
    /// </summary>
    public class AccountBalanceProcessingService
    {
        private readonly ILogger<AccountBalanceProcessingService> _logger;
        
        public AccountBalanceProcessingService(ILogger<AccountBalanceProcessingService> logger)
        {
            _logger = logger;
        }

        public Task<bool> ProcessBalanceUpdateAsync(AccountBalanceUpdateMessage message)
        {
            try
            {
                // In TransactionService, we just log receipt of the message
                // The actual balance update happens in UserAccountService
                _logger.LogInformation("Received balance update confirmation");
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing balance update");
                return Task.FromResult(false);
            }
        }
    }
}