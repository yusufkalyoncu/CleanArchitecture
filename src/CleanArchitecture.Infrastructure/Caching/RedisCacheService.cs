using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
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

            var result = JsonSerializer.Deserialize<T>(value.ToString());
            return result;
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
            
            _logger.LogDebug("Redis cache removed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key from Redis: {Key}", key);
        }
    }

    public async Task RemoveAsync(List<string> keys, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)GetRedisKey(k)).ToArray();
            await _database.KeyDeleteAsync(redisKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing multiple cache keys from Redis");
        }
    }

    public Task<List<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisPattern = GetRedisKey(pattern);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            var keys = server.Keys(pattern: redisPattern, pageSize: 1000)
                .Select(k => k.ToString().Replace(_instanceName, string.Empty))
                .ToList();

            return Task.FromResult(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting keys by pattern from Redis: {Pattern}", pattern);
            return Task.FromResult(new List<string>());
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await GetKeysByPatternAsync(pattern, cancellationToken);
            await RemoveAsync(keys, cancellationToken);
            
            _logger.LogDebug("Redis cache removed by pattern: {Pattern}, Count: {Count}", pattern, keys.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache by pattern from Redis: {Pattern}", pattern);
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

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);
        
        if (cachedValue != null)
        {
            return cachedValue;
        }

        var value = await factory();
        await SetAsync(key, value, expiration, cancellationToken);
        
        return value;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await GetKeysByPatternAsync("*", cancellationToken);
            await RemoveAsync(keys, cancellationToken);
            
            _logger.LogInformation("All Redis cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Redis cache");
        }
    }

    private string GetRedisKey(string key)
    {
        return $"{_instanceName}{key}";
    }
}