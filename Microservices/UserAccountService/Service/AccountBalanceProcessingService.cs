using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service // Changed from Services to Service
{
    /// <summary>
    /// A service to process account balance updates directly using the API endpoint
    /// </summary>
    public class AccountBalanceProcessingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AccountBalanceProcessingService> _logger;

        public AccountBalanceProcessingService(
            IHttpClientFactory httpClientFactory,
            ILogger<AccountBalanceProcessingService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("InternalApi");
            _httpClient.BaseAddress = new Uri("http://localhost:80"); 
            _logger = logger;
        }

        public async Task<bool> ProcessBalanceUpdateAsync(AccountBalanceUpdateMessage message)
        {
            _logger.LogInformation("Processing balance update request for account, transactionId");
                           
            try
            {
                // Fix the BaseAddress to use the container hostname in Docker environment
                var baseAddress = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" 
                    ? "http://localhost:80" 
                    : "http://user-account-service:80";
                    
                _httpClient.BaseAddress = new Uri(baseAddress);
                _logger.LogInformation("Using configured base address");
                
                var request = new AccountBalanceRequest
                {
                    Amount = message.Amount,
                    TransactionId = message.TransactionId,
                    TransactionType = message.TransactionType,
                    IsAdjustment = message.IsAdjustment
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request), 
                    Encoding.UTF8, 
                    "application/json");
                
                _logger.LogInformation("Sending balance update request");
                
                var response = await _httpClient.PutAsync(
                    $"/api/Account/{message.AccountId}/balance", 
                    content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Balance update successful");
                    return true;
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to process balance update: StatusCode={StatusCode}", 
                    response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Account not found - won't retry");
                    return true;  // Acknowledge permanent errors
                }
                
                return false;  // Retry for retriable errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance update");
                return false;  // Retry on exception
            }
        }
    }
}