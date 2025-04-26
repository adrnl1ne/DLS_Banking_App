using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransactionService.Models;

namespace TransactionService.Services;

public class UserAccountClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserAccountClientService> _logger;

    public UserAccountClientService(HttpClient httpClient, IConfiguration configuration, ILogger<UserAccountClientService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Use token from configuration
        var serviceToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0cmFuc2FjdGlvbi1zZXJ2aWNlIiwicm9sZSI6InNlcnZpY2UiLCJqdGkiOiJjNGEwMzRjYy1iMDE4LTQxYTYtOTNmMi02MDc5MDQ1MWU1OWEiLCJpc3MiOiJCYW5raW5nQXBwIiwic2NvcGVzIjpbInJlYWQ6YWNjb3VudHMiLCJ1cGRhdGU6YWNjb3VudC1iYWxhbmNlIl0sImV4cCI6MTc2MTI0NjM0NSwiYXVkIjoiVXNlckFjY291bnRBUEkifQ.xiE7sJOYZWizg-cvk_yKya4-vfaXUV9BDTXaJx5QgJE";
        _httpClient.BaseAddress = new Uri(configuration["Services:UserAccountService"] ?? "http://user-account-service:80");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        
        logger.LogInformation("UserAccountClientService initialized with BaseAddress: {BaseAddress}, Token: {Token}", 
            _httpClient.BaseAddress, serviceToken);
    }

    public async Task<Account?> GetAccountAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/Account/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Account>();
    }

    public async Task UpdateBalanceAsync(int accountId, decimal newBalance)
    {
        try
        {
            _logger.LogInformation("Updating account {AccountId} balance to {NewBalance}", accountId, newBalance);
            
            // Create a proper AccountBalanceRequest based on the service's expected format
            var balanceRequest = new
            {
                Amount = newBalance,
                TransactionId = Guid.NewGuid().ToString() // Generate a unique ID for idempotency
            };
            
            // Serialize with proper content type
            var content = new StringContent(
                JsonSerializer.Serialize(balanceRequest),
                Encoding.UTF8,
                "application/json");
                
            // Make the API call
            var response = await _httpClient.PutAsync($"/api/Account/{accountId}/balance", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update balance for account {AccountId}. Status: {Status}, Error: {Error}", 
                    accountId, response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to update balance for account {accountId}. Status: {response.StatusCode}, Error: {errorContent}");
            }
            
            _logger.LogInformation("Successfully updated account {AccountId} balance to {NewBalance}", accountId, newBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating balance for account {AccountId} to {NewBalance}", accountId, newBalance);
            throw;
        }
    }
}
