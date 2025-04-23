using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TransactionService.Clients;

public class UserAccountClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceToken;

    public UserAccountClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _serviceToken = configuration["TRANSACTION_SERVICE_TOKEN"] ?? throw new InvalidOperationException("Service token must be configured");
        _httpClient.BaseAddress = new Uri("http://user-account-service:80");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _serviceToken);
    }

    public async Task<Account> GetAccountAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/account/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Account>() ?? throw new Exception($"Failed to deserialize account {id}");
    }

    public async Task UpdateBalanceAsync(int id, decimal newBalance)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/account/{id}/balance", newBalance);
        response.EnsureSuccessStatusCode();
    }
}

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int UserId { get; set; }
}