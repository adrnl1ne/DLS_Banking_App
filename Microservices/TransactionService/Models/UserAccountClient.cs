using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransactionService.Models;

namespace TransactionService.Clients
{
    public class UserAccountClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UserAccountClient> _logger;

        public UserAccountClient(HttpClient httpClient, ILogger<UserAccountClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<Account> GetAccountAsync(string accountId)
        {
            try
            {
                _logger.LogInformation($"Fetching account {accountId} from User Account Service");
                var response = await _httpClient.GetAsync($"/api/accounts/{accountId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<Account>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching account {accountId}");
                throw;
            }
        }

        public async Task<bool> VerifyAccountOwnershipAsync(string accountId, int userId)
        {
            try
            {
                _logger.LogInformation($"Verifying ownership of account {accountId} for user {userId}");
                var response = await _httpClient.GetAsync($"/api/accounts/{accountId}/verify/{userId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying account ownership: {accountId}, user: {userId}");
                return false;
            }
        }
    }
}

namespace TransactionService.Models
{
    public class Account
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int UserId { get; set; }
    }
}