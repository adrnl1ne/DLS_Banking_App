using StackExchange.Redis;

namespace TransactionService.Infrastructure.Redis;

public class RedisClient : IRedisClient, IDisposable
{
    private readonly ILogger<RedisClient> _logger;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisClient(ILogger<RedisClient> logger, string connectionString = "redis:6379")
    {
        _logger = logger;
        try
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
            _logger.LogInformation("Redis client initialized successfully. Connected to {ConnectionString}",
                connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Redis client");
            throw;
        }
    }

    public async Task<string> GetAsync(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            return (value.HasValue ? value.ToString() : null) ?? throw new InvalidOperationException();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving key {Key} from Redis", key);
            throw;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan expiry)
    {
        try
        {
            await _db.StringSetAsync(key, value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key} in Redis", key);
            throw;
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}