namespace CleanArchitecture.Application.Abstractions.Caching;

public interface IDistributedCacheService : ICacheService
{
    /// <summary>
    /// Atomically gets the value of a key, compares it with an expected value,
    /// and removes the key if the values match.
    /// </summary>
    Task<bool> CompareAndRemoveAsync<T>(string key, T expectedValue,
        CancellationToken cancellationToken = default);
    
    // --- Set (SET) Operations ---
    
    /// <summary>
    /// Adds an element to a set (SET). (SADD command - O(1))
    /// </summary>
    Task SetAddAsync(string key, string value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes an element from a set (SET). (SREM command - O(1))
    /// </summary>
    Task SetRemoveAsync(string key, string value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Returns the number of elements in a set (SET). (SCARD command - O(1))
    /// </summary>
    Task<long> SetLengthAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Returns all elements in a set (SET). (SMEMBERS command)
    /// </summary>
    Task<string[]> SetMembersAsync(string key, CancellationToken cancellationToken = default);
    
    // --- Sorted Set (ZSET) Operations ---
    
    /// <summary>
    /// Adds a member with a specific score to a sorted set (ZSET). (ZADD command)
    /// </summary>
    Task SortedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific member from a sorted set (ZSET). (ZREM command)
    /// </summary>
    Task<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of elements in a sorted set (ZSET). (ZCARD command - O(1))
    /// </summary>
    Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all members in a sorted set (ZSET) within a specified score range. (ZREMRANGEBYSCORE command)
    /// </summary>
    Task<long> SortedSetRemoveRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns members in a sorted set (ZSET) within a specified score range. (ZRANGEBYSCORE command)
    /// </summary>
    Task<string[]> SortedSetRangeByScoreAsync(string key, double minScore = double.NegativeInfinity, double maxScore = double.PositiveInfinity, CancellationToken cancellationToken = default);
}