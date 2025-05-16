using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;
using System.Text;
using System.Text.Json;

namespace UserAccountService.Services
{
    /// <summary>
    /// A service to process account balance updates directly using the DB context
    /// without going through the controller layer
    /// </summary>
    public class AccountBalanceProcessingService
    {
        private readonly ILogger<AccountBalanceProcessingService> _logger;
        private readonly HttpClient _httpClient;

        public AccountBalanceProcessingService(
            ILogger<AccountBalanceProcessingService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("InternalApi");
            // Set base address to internal service endpoint
            _httpClient.BaseAddress = new Uri("http://localhost:80"); // Usually the service itself
        }

        public async Task<bool> ProcessBalanceUpdateAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation(
                "Processing balance update for account {AccountId}, transaction {TransactionId}",
                message.AccountId, message.TransactionId);
            
            try
            {
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.TransactionType,
                    IsAdjustment = message.IsAdjustment
                };
                
                // Use HTTP client to call the internal API endpoint
                var content = new StringContent(
                    JsonSerializer.Serialize(request), 
                    Encoding.UTF8, 
                    "application/json");
                
                var response = await _httpClient.PutAsync(
                    $"/api/Account/{message.AccountId}/balance", 
                    content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Balance updated successfully for account {AccountId}",
                        message.AccountId);
                    return true;
                }
                
                // For permanent errors like 404, we don't want to reprocess
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Account {AccountId} not found", message.AccountId);
                    return true;  // Return true to acknowledge message
                }
                
                _logger.LogWarning(
                    "Failed to update balance for account {AccountId}. Status: {StatusCode}",
                    message.AccountId, response.StatusCode);
                
                // For other errors, we might want to retry
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Exception occurred while processing balance update for account {AccountId}", 
                    message.AccountId);
                return false;  // Retry
            }
        }
    }
}