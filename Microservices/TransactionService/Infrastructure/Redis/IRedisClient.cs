namespace TransactionService.Infrastructure.Redis;

public interface IRedisClient
{
    Task<string> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan expiry);
}