using TransactionService.Models;

namespace TransactionService.Services;

public class UserAccountClientService
{
    private readonly HttpClient _httpClient;

    public UserAccountClientService(HttpClient httpClient, IConfiguration configuration, ILogger<UserAccountClientService> logger)
    {
        _httpClient = httpClient;
        var serviceToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0cmFuc2FjdGlvbi1zZXJ2aWNlIiwicm9sZSI6InNlcnZpY2UiLCJqdGkiOiJjNGEwMzRjYy1iMDE4LTQxYTYtOTNmMi02MDc5MDQ1MWU1OWEiLCJpc3MiOiJCYW5raW5nQXBwIiwic2NvcGVzIjpbInJlYWQ6YWNjb3VudHMiLCJ1cGRhdGU6YWNjb3VudC1iYWxhbmNlIl0sImV4cCI6MTc2MTI0NjM0NSwiYXVkIjoiVXNlckFjY291bnRBUEkifQ.xiE7sJOYZWizg-cvk_yKya4-vfaXUV9BDTXaJx5QgJE" ?? throw new InvalidOperationException("Service token must be configured");
        _httpClient.BaseAddress = new Uri(configuration["Services:UserAccountService"] ?? "http://user-account-service:80");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
        logger.LogInformation("UserAccountClientService initialized with BaseAddress: {BaseAddress}, Token: {Token}", _httpClient.BaseAddress, serviceToken);
    }
    public async Task<Account?> GetAccountAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/Account/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Account>();
    }

    public async Task UpdateBalanceAsync(int accountId, decimal newBalance)
    {
        var response = await _httpClient.PutAsync($"/api/Account/{accountId}/balance?newBalance={newBalance}", null);
        response.EnsureSuccessStatusCode();
    }
}
