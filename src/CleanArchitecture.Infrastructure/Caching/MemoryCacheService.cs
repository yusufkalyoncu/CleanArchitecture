using CleanArchitecture.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class MemoryCacheService(IMemoryCache memoryCache) : ICacheService, IDisposable
{
    private readonly object _lock = new();
    private CancellationTokenSource _cancellationTokenSource = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.SetAbsoluteExpiration(expiration.Value);
        }

        lock (_lock)
        {
            options.AddExpirationToken(new CancellationChangeToken(_cancellationTokenSource.Token));
        }

        memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            memoryCache.Remove(key);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(memoryCache.TryGetValue(key, out _));
    }

    public Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        return memoryCache.GetOrCreateAsync<T>(key, entry =>
        {
            if (expiration.HasValue)
            {
                entry.SetAbsoluteExpiration(expiration.Value);
            }
            
            lock (_lock)
            {
                entry.AddExpirationToken(new CancellationChangeToken(_cancellationTokenSource.Token));
            }
            
            return factory();
        });
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _cancellationTokenSource.Dispose();
        }
    }
}