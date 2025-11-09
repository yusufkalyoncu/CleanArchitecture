using System.Diagnostics;
using CleanArchitecture.Application.Abstractions.Locking;
using CleanArchitecture.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure.Locking;

public class RedisLockManager(
    IConnectionMultiplexer redis,
    ILogger<RedisLockManager> logger,
    IOptions<RedisOptions> options)
    : IDistributedLockManager
{
    private readonly IDatabase _database = redis.GetDatabase(options.Value.Database);
    private readonly string _instanceName = options.Value.InstanceName;

    private string GetRedisKey(string resource) => $"{_instanceName}:lock:{resource}";
    private string CreateLockToken() => Guid.NewGuid().ToString("N");

    public async Task<bool> TryExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        return await TryExecuteWithLockAsync(
            resource,
            expiry,
            TimeSpan.Zero,
            TimeSpan.Zero,
            action,
            cancellationToken);
    }

    public async Task<bool> TryExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan waitTime,
        TimeSpan retryTime,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        var redisKey = GetRedisKey(resource);
        var lockToken = CreateLockToken();

        var stopwatch = Stopwatch.StartNew();
        bool acquired = false;
        
        do
        {
            if (cancellationToken.IsCancellationRequested) break;

            acquired = await _database.LockTakeAsync(redisKey, lockToken, expiry);
            if (acquired)
            {
                break;
            }

            if (waitTime == TimeSpan.Zero || retryTime == TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(retryTime, cancellationToken);
        }
        while (stopwatch.Elapsed < waitTime);

        stopwatch.Stop();

        if (!acquired)
        {
            logger.LogWarning("Failed to acquire lock (timeout or fail-fast): {Resource}", resource);
            return false;
        }
        
        try
        {
            await action(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing action with lock: {Resource}", resource);
            throw;
        }
        finally
        {
            await _database.LockReleaseAsync(redisKey, lockToken);
        }
    }
}