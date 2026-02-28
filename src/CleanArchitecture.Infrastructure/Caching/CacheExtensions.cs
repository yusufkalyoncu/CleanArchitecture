using CleanArchitecture.Application.Abstractions.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure.Caching;

public static class CacheExtensions
{
    public static IServiceCollection AddRedisConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        if (redisOptions == null)
        {
            throw new InvalidOperationException("RedisOptions section is missing in configuration.");
        }

        var redisConfig = ConfigurationOptions.Parse(redisOptions.ConnectionString);

        redisConfig.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
        redisConfig.ConnectTimeout = redisOptions.ConnectTimeout;
        redisConfig.SyncTimeout = redisOptions.SyncTimeout;
        redisConfig.DefaultDatabase = redisOptions.Database;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));

        return services;
    }

    public static IServiceCollection AddCacheServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<IDistributedCacheService, RedisCacheService>();

        return services;
    }
}