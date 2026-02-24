using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Caching;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class RedisCacheBatch(IBatch batch, string instanceName) : ICacheBatch
{
    private readonly List<Task> _tasks = [];

    private string GetRedisKey(string key) => $"{instanceName}:{key}";

    public void SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var json = JsonSerializer.Serialize(value);
        _tasks.Add(batch.StringSetAsync(GetRedisKey(key), json, expiration));
    }

    public void RemoveAsync(string key)
    {
        _tasks.Add(batch.KeyDeleteAsync(GetRedisKey(key)));
    }

    public void SortedSetAddAsync(string key, string member, double score)
    {
        _tasks.Add(batch.SortedSetAddAsync(GetRedisKey(key), member, score));
    }

    public void SortedSetRemoveAsync(string key, string member)
    {
        _tasks.Add(batch.SortedSetRemoveAsync(GetRedisKey(key), member));
    }

    public async Task ExecuteAsync()
    {
        batch.Execute();
        await Task.WhenAll(_tasks);
    }
}