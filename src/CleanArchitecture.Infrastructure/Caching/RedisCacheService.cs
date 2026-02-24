using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class RedisCacheService : IDistributedCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly RedisOptions _options;
    private readonly string _instanceName;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        IOptions<RedisOptions> options)
    {
        _redis = redis;
        _logger = logger;
        _options = options.Value;
        _instanceName = _options.InstanceName;
        _database = _redis.GetDatabase(_options.Database);
    }

    #region Basic Cache Operations (ICacheService)
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var value = await _database.StringGetAsync(redisKey);

            if (!value.HasValue)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key from Redis: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var json = JsonSerializer.Serialize(value);
            var cacheExpiration = expiration ?? _options.DefaultExpiration;

            await _database.StringSetAsync(redisKey, json, cacheExpiration);

            _logger.LogDebug("Redis cache set: {Key} with expiration {Expiration}", key, cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key in Redis: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            await _database.KeyDeleteAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key from Redis: {Key}", key);
        }
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)GetRedisKey(k)).ToArray();
            if (redisKeys.Length > 0)
            {
                await _database.KeyDeleteAsync(redisKeys);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing multiple cache keys from Redis");
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            return await _database.KeyExistsAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists in Redis: {Key}", key);
            return false;
        }
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var rawValue = await _database.StringGetAsync(redisKey);

            if (rawValue.HasValue)
            {
                return JsonSerializer.Deserialize<T>(rawValue.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key in GetOrSetAsync (will run factory): {Key}", key);
        }

        var value = await factory();

        await SetAsync(key, value, expiration, cancellationToken);

        return value;
    }
    
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"{_instanceName}:*";
            var keysToDelete = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(_database.Database, pattern, pageSize: 1000)
                               .WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Redis key clearing was cancelled.");
                    break;
                }

                keysToDelete.Add(key);
            }

            if (keysToDelete.Count > 0)
            {
                await _database.KeyDeleteAsync(keysToDelete.ToArray());
                _logger.LogInformation("Cleared {Count} keys with pattern {Pattern} from Redis database {DbNum}",
                    keysToDelete.Count, pattern, _database.Database);
            }
            else
            {
                _logger.LogInformation("No keys with pattern {Pattern} found to clear.", pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Redis database by pattern");
        }
    }
    
    #endregion
    
    #region Atomic & Advanced Operations

    public async Task<bool> CompareAndRemoveAsync<T>(string key, T expectedValue, CancellationToken cancellationToken = default)
    {
        const string script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await ExecuteScriptAsync<long>(script, [key], [JsonSerializer.Serialize(expectedValue)], cancellationToken);
        return result == 1;
    }

    public async Task<T> ExecuteScriptAsync<T>(string script, string[] keys, object[] args, CancellationToken cancellationToken = default)
    {
        var redisKeys = keys.Select(k => (RedisKey)GetRedisKey(k)).ToArray();
        var redisArgs = args.Select(a => (RedisValue)a.ToString()!).ToArray();

        var result = await _database.ScriptEvaluateAsync(script, redisKeys, redisArgs);
        
        if (result.IsNull) return default!;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    public ICacheBatch CreateBatch() => new RedisCacheBatch(_database.CreateBatch(), _instanceName);

    #endregion

    #region Set Operations (Unordered)

    public async Task SetAddAsync(string key, string value, CancellationToken cancellationToken = default) 
        => await _database.SetAddAsync(GetRedisKey(key), value);

    public async Task SetRemoveAsync(string key, string value, CancellationToken cancellationToken = default) 
        => await _database.SetRemoveAsync(GetRedisKey(key), value);

    public async Task<long> GetSetLengthAsync(string key, CancellationToken cancellationToken = default) 
        => await _database.SetLengthAsync(GetRedisKey(key));

    public async Task<string[]> GetSetMembersAsync(string key, CancellationToken cancellationToken = default)
    {
        var members = await _database.SetMembersAsync(GetRedisKey(key));
        return members.Select(m => m.ToString()).ToArray();
    }

    #endregion

    #region Ordered Set Operations

    public async Task OrderedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken = default)
        => await _database.SortedSetAddAsync(GetRedisKey(key), member, score);

    public async Task<bool> OrderedSetRemoveAsync(string key, string member, CancellationToken cancellationToken = default)
        => await _database.SortedSetRemoveAsync(GetRedisKey(key), member);

    public async Task<long> GetOrderedSetLengthAsync(string key, CancellationToken cancellationToken = default)
        => await _database.SortedSetLengthAsync(GetRedisKey(key));

    public async Task<long> RemoveOrderedSetRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken = default)
        => await _database.SortedSetRemoveRangeByScoreAsync(GetRedisKey(key), minScore, maxScore);

    public async Task<string[]> GetOrderedSetRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken = default)
    {
        var result = await _database.SortedSetRangeByScoreAsync(GetRedisKey(key), minScore, maxScore);
        return result.Select(m => m.ToString()).ToArray();
    }

    #endregion

    private string GetRedisKey(string key) => $"{_instanceName}:{key}";
}