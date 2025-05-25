using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TransactionService.Infrastructure.Security;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services;

public class UserAccountClientService : IUserAccountClient
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
        logger.LogInformation("UserAccountClientService initialized");
    }

    public async Task<Account?> GetAccountAsync(int id)
    {
        try
        {
            _logger.LogInformation("Getting account details");
            
            var response = await _httpClient.GetAsync($"/api/Account/{id}");
            response.EnsureSuccessStatusCode();
            
            var account = await response.Content.ReadFromJsonAsync<Account>();
            return account;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving account");
            throw;
        }
    }

    public async Task UpdateBalanceAsync(int accountId, AccountBalanceRequest balanceRequest)
    {
        try
        {
            _logger.LogInformation("Updating account balance with operation type");
                
            // Serialize with proper content type
            var content = new StringContent(
                JsonSerializer.Serialize(balanceRequest),
                Encoding.UTF8,
                "application/json");
                    
            // Make the API call
            var response = await _httpClient.PutAsync($"/api/Account/{accountId}/balance", content);
            
            if (!response.IsSuccessStatusCode)
            {
                LogSanitizedError();
                throw new InvalidOperationException($"Failed to update account balance. Status: {response.StatusCode}");
            }
            
            _logger.LogInformation("Successfully updated account balance");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating account balance");
            throw;
        }
    }

    private void LogSanitizedError()
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

            _logger.LogError("Account service returned error. Type: {ErrorType}, Status: {ErrorStatus}",
                errorType, errorStatus);
        }
        catch
        {
            _logger.LogError("Account service request failed");
        }
    }
}
