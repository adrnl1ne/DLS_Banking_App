namespace TransactionService.Models;

public class UserAccountClient
{
    private readonly HttpClient _httpClient;

    public UserAccountClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var serviceToken = configuration["TRANSACTION_SERVICE_TOKEN"] ?? throw new InvalidOperationException("Service token must be configured");
        _httpClient.BaseAddress = new Uri("http://user-account-service:80");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
    }

    public async Task<Account?> GetAccountAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/account/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Account>();
    }

    public async Task UpdateBalanceAsync(int accountId, decimal newBalance)
    {
        var response = await _httpClient.PutAsync($"/api/account/{accountId}/balance?newBalance={newBalance}", null);
        response.EnsureSuccessStatusCode();
    }
}
