using TransactionService.Models;

namespace TransactionService.Clients
{
    public class UserAccountClient(HttpClient httpClient, ILogger<UserAccountClient> logger)
    {
        public async Task<Account?> GetAccountAsync(string accountId)
        {
            try
            {
                logger.LogInformation($"Fetching account {accountId} from User Account Service");
                var response = await httpClient.GetAsync($"/api/accounts/{accountId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<Account>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error fetching account {accountId}");
                throw;
            }
        }

        public async Task<bool> VerifyAccountOwnershipAsync(string accountId, int userId)
        {
            try
            {
                logger.LogInformation($"Verifying ownership of account {accountId} for user {userId}");
                var response = await httpClient.GetAsync($"/api/accounts/{accountId}/verify/{userId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error verifying account ownership: {accountId}, user: {userId}");
                return false;
            }
        }
    }
}

namespace TransactionService.Models
{
    public abstract class Account
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int UserId { get; set; }
    }
}