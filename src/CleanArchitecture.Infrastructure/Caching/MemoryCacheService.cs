using CleanArchitecture.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class MemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    private readonly HashSet<string> _keys = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
            options.SetAbsoluteExpiration(expiration.Value);

        memoryCache.Set(key, value, options);

        lock (_keys)
            _keys.Add(key);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(key);
        lock (_keys)
            _keys.Remove(key);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(List<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            memoryCache.Remove(key);

        lock (_keys)
        {
            foreach (var key in keys)
                _keys.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        lock (_keys)
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            var matched = _keys.Where(k => System.Text.RegularExpressions.Regex.IsMatch(k, regexPattern)).ToList();
            return Task.FromResult(matched);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var keys = await GetKeysByPatternAsync(pattern, cancellationToken);
        await RemoveAsync(keys, cancellationToken);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(memoryCache.TryGetValue(key, out _));
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue is not null)
            return cachedValue;

        var newValue = await factory();

        await SetAsync(key, newValue, expiration, cancellationToken);

        return newValue;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_keys)
        {
            foreach (var key in _keys.ToList())
                memoryCache.Remove(key);

            _keys.Clear();
        }

        return Task.CompletedTask;
    }
}