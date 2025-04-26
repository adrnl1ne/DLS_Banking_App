using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Security;
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
        
        // Get token from configuration (secure way)
        var serviceToken = configuration["ServiceAuthentication:Token"] 
            ?? throw new InvalidOperationException("ServiceAuthentication:Token is not configured");
        _httpClient.BaseAddress = new Uri(configuration["Services:UserAccountService"] ?? "http://user-account-service:80");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        
        // Don't log the token, just log the base address
        logger.LogInformation("UserAccountClientService initialized with BaseAddress: {BaseAddress}", 
            _httpClient.BaseAddress);
    }

    public async Task<Account?> GetAccountAsync(int id)
    {
        try
        {
            _logger.LogInformation("Getting account details for account ID {AccountId}", id);
            
            var response = await _httpClient.GetAsync($"/api/Account/{id}");
            response.EnsureSuccessStatusCode();
            
            var account = await response.Content.ReadFromJsonAsync<Account>();
            return account;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving account {AccountId}", id);
            throw;
        }
    }

    public async Task UpdateBalanceAsync(int accountId, AccountBalanceRequest balanceRequest)
    {
        try
        {
            _logger.LogInformation("Updating account {AccountId} balance with type {TransactionType}", 
                LogSanitizer.MaskAccountId(accountId), balanceRequest.TransactionType);
                
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
                LogSanitizedError(errorContent, accountId);
                throw new InvalidOperationException($"Failed to update balance for account {LogSanitizer.MaskAccountId(accountId)}. Status: {response.StatusCode}");
            }
            
            _logger.LogInformation("Successfully updated account {AccountId} balance", 
                LogSanitizer.MaskAccountId(accountId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating balance for account {AccountId}", 
                LogSanitizer.MaskAccountId(accountId));
            throw;
        }
    }

    private void LogSanitizedError(string errorContent, int accountId)
    {
        try
        {
            // Try to extract just essential info from the error
            var errorType = "Unknown";
            var errorStatus = "Unknown";
            
            try
            {
                using var doc = JsonDocument.Parse(errorContent);
                if (doc.RootElement.TryGetProperty("title", out var title))
                    errorType = title.GetString() ?? "Unknown";
                    
                if (doc.RootElement.TryGetProperty("status", out var status))
                    errorStatus = status.GetInt32().ToString();
            }
            catch
            {
                // If parsing fails, use generic message
            }
            
            _logger.LogError("Failed to update balance for account {AccountId}. Error type: {ErrorType}, Status: {ErrorStatus}", 
                LogSanitizer.MaskAccountId(accountId), errorType, errorStatus);
        }
        catch
        {
            // Fallback to very limited info if even the sanitization fails
            _logger.LogError("Failed to update balance for account {AccountId}. (Error details sanitized)", 
                LogSanitizer.MaskAccountId(accountId));
        }
    }
    
    private string MaskId(int id)
    {
        // Use the utility method from LogSanitizer instead of duplicating code
        return LogSanitizer.MaskAccountId(id);
    }

    private string MaskAmount(decimal amount)
    {
        // Only show that there was an amount, not the specific value
        return "***.**";
    }
}
