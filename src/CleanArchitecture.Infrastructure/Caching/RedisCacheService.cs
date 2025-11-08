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

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
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

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
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

    public async Task<bool> CompareAndRemoveAsync<T>(string key, T expectedValue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var serializedExpectedValue = JsonSerializer.Serialize(expectedValue);

            const string script = @"
                local currentValue = redis.call('GET', KEYS[1])
                if currentValue == ARGV[1] then
                    redis.call('DEL', KEYS[1])
                    return 1
                else
                    return 0
                end";

            var result = await _database.ScriptEvaluateAsync(
                script,
                [redisKey],
                [serializedExpectedValue]);

            return (long)result == 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CompareAndRemoveAsync<{T}>: {Key}", typeof(T).Name, key);
            return false;
        }
    }

    public async Task SetAddAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            await _database.SetAddAsync(redisKey, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to set in Redis: {Key}", key);
        }
    }

    public async Task SetRemoveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            await _database.SetRemoveAsync(redisKey, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from set in Redis: {Key}", key);
        }
    }

    public async Task<long> SetLengthAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            return await _database.SetLengthAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting set length from Redis: {Key}", key);
            return 0;
        }
    }

    public async Task<string[]> SetMembersAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var members = await _database.SetMembersAsync(redisKey);
            return members.Select(m => m.ToString()).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting set members from Redis: {Key}", key);
            return [];
        }
    }

    public async Task SortedSetAddAsync(string key, string member, double score,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            await _database.SortedSetAddAsync(redisKey, member, score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting sorted set cache key in Redis: {Key}", key);
        }
    }

    public async Task<bool> SortedSetRemoveAsync(string key, string member,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            return await _database.SortedSetRemoveAsync(redisKey, member);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing sorted set cache key in Redis: {Key}", key);
            return false;
        }
    }

    public async Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            return await _database.SortedSetLengthAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sorted set length in Redis: {Key}", key);
            return 0;
        }
    }

    public async Task<long> SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            return await _database.SortedSetRemoveRangeByScoreAsync(redisKey, minScore, maxScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SortedSetRemoveRangeByScoreAsync: {Key}", key);
            return 0;
        }
    }

    public async Task<string[]> SortedSetRangeByScoreAsync(string key, double minScore = Double.NegativeInfinity,
        double maxScore = Double.PositiveInfinity, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var result = await _database.SortedSetRangeByScoreAsync(redisKey, minScore, maxScore);
            return result.Select(m => m.ToString()).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SortedSetRangeByScoreAsync: {Key}", key);
            return [];
        }
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

    private string GetRedisKey(string key)
    {
        return $"{_instanceName}:{key}";
    }
}