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

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
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

    public async Task<bool> SetAsync(string key, string value, TimeSpan expiry, string condition)
    {
        try
        {
            When when = condition.ToUpperInvariant() switch
            {
                "NX" => When.NotExists,
                "XX" => When.Exists,
                _ => When.Always
            };
            return await _db.StringSetAsync(key, value, expiry, when);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting key {Key} with condition {Condition} in Redis", key, condition);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of key {Key} in Redis", key);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            return await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key {Key} in Redis", key);
            throw;
        }
    }

    public async Task ExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            await _db.KeyExpireAsync(key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting expiry for key {Key} in Redis", key);
            throw;
        }
    }

    public async Task HashSetAsync(string key, string field, string value)
    {
        try
        {
            await _db.HashSetAsync(key, field, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hash field {Field} for key {Key} in Redis", field, key);
            throw;
        }
    }

    public async Task HashSetAsync(string key, Dictionary<string, string> fields)
    {
        try
        {
            var hashFields = fields.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray();
            await _db.HashSetAsync(key, hashFields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hash fields for key {Key} in Redis", key);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
    {
        try
        {
            var hashFields = await _db.HashGetAllAsync(key);
            return hashFields.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all hash fields for key {Key} in Redis", key);
            throw;
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
