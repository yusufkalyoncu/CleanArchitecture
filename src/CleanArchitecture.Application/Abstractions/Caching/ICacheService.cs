namespace CleanArchitecture.Application.Abstractions.Caching;

public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in cache with optional expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached value by key
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple cached values by keys
    /// </summary>
    Task RemoveAsync(List<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all keys matching the pattern (supports wildcards like "user:*")
    /// </summary>
    Task<List<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached values matching the pattern
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or sets a value in cache. If the key doesn't exist, the factory function is called to create the value.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached values
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}