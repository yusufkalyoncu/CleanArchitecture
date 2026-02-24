namespace CleanArchitecture.Application.Abstractions.Caching;

public interface ICacheBatch
{
    void SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    void RemoveAsync(string key);
    void SortedSetAddAsync(string key, string member, double score);
    void SortedSetRemoveAsync(string key, string member);
    Task ExecuteAsync();
}