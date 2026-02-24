using CleanArchitecture.Application.Abstractions.Option;

namespace CleanArchitecture.Infrastructure.Caching;

public sealed class RedisOptions : IAppOption
{
    public const string SectionName = "RedisOptions";

    public string Host { get; init; } = null!;
    public int Port { get; init; }
    public string InstanceName { get; init; } = null!;
    public int Database { get; init; }
    public bool AbortOnConnectFail { get; init; }
    public int ConnectTimeout { get; init; }
    public int SyncTimeout { get; init; }
    public TimeSpan DefaultExpiration { get; init; }
    
    public string ConnectionString => $"{Host}:{Port}";
}