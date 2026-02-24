namespace CleanArchitecture.Application.Abstractions.Caching;

public interface IDistributedCacheService : ICacheService
{
    /// <summary>
    /// Atomically gets the value of a key, compares it with an expected value,
    /// and removes the key only if the values match.
    /// </summary>
    Task<bool> CompareAndRemoveAsync<T>(string key, T expectedValue, CancellationToken cancellationToken = default);

    // --- Unordered Set Operations ---
    
    /// <summary>
    /// Adds a unique member to a collection.
    /// </summary>
    Task SetAddAsync(string key, string value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a member from a collection.
    /// </summary>
    Task SetRemoveAsync(string key, string value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the total number of unique members in a collection.
    /// </summary>
    Task<long> GetSetLengthAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all members of a collection.
    /// </summary>
    Task<string[]> GetSetMembersAsync(string key, CancellationToken cancellationToken = default);
    
    // --- Ordered Set Operations (Ranked Collections) ---
    
    /// <summary>
    /// Adds a member to an ordered collection with a specific score for ranking.
    /// </summary>
    Task OrderedSetAddAsync(string key, string member, double score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific member from an ordered collection.
    /// </summary>
    Task<bool> OrderedSetRemoveAsync(string key, string member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of members in an ordered collection.
    /// </summary>
    Task<long> GetOrderedSetLengthAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes members within a specific score range from an ordered collection.
    /// </summary>
    Task<long> RemoveOrderedSetRangeByScoreAsync(string key, double minScore, double maxScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets members within a specific score range from an ordered collection.
    /// </summary>
    Task<string[]> GetOrderedSetRangeByScoreAsync(string key, double minScore = double.NegativeInfinity, double maxScore = double.PositiveInfinity, CancellationToken cancellationToken = default);

    // --- Atomic Scripting & Pipelining ---

    /// <summary>
    /// Executes a provider-specific script atomically. 
    /// Use this for complex operations that must be performed as a single unit.
    /// </summary>
    Task<T> ExecuteScriptAsync<T>(string script, string[] keys, object[] args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a batch to group multiple operations and execute them in a single network round-trip.
    /// </summary>
    ICacheBatch CreateBatch();
}