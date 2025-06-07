namespace TransactionService.Infrastructure.Redis;

public interface IRedisClient
{
    Task<string> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan expiry);
    Task<bool> SetAsync(string key, string value, TimeSpan expiry, string condition);
    Task<bool> ExistsAsync(string key);
    Task<bool> DeleteAsync(string key);
    Task ExpireAsync(string key, TimeSpan expiry);
    
    // Hash operations for atomic updates
    Task HashSetAsync(string key, string field, string value);
    Task HashSetAsync(string key, Dictionary<string, string> fields);
    Task<Dictionary<string, string>> HashGetAllAsync(string key);
}