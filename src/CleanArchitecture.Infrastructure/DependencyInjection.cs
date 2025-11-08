using System.Reflection;
using System.Text;
using CleanArchitecture.Application.Abstractions.Caching;
using CleanArchitecture.Application.Abstractions.Database;
using CleanArchitecture.Application.Abstractions.DomainEvents;
using CleanArchitecture.Application.Abstractions.Option;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Caching;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddServices()
            .AddAppOptions(configuration, typeof(PostgresOptions).Assembly)
            .AddDatabase(configuration)
            .AddRedisConfiguration(configuration)
            .AddCacheServices();

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        return services;
    }
    
    private static IServiceCollection AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var optionTypes = assembly
            .GetTypes()
            .Where(t => typeof(IAppOption).IsAssignableFrom(t) 
                        && t is { IsClass: true, IsAbstract: false });

        foreach (var type in optionTypes)
        {
            var sectionName = type.GetField("SectionName")?.GetValue(null)?.ToString();

            if (string.IsNullOrWhiteSpace(sectionName))
                continue;
            
            var method = typeof(OptionsConfigurationServiceCollectionExtensions)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == nameof(OptionsConfigurationServiceCollectionExtensions.Configure)
                            && m.GetParameters().Length == 2)
                .MakeGenericMethod(type);

            method.Invoke(null, [services, configuration.GetSection(sectionName)]);
        }

        return services;
    }
    
    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresOptions = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>();
        if (postgresOptions is null)
        {
            throw new InvalidOperationException("Postgres options are not configured.");
        }
        
        services.AddDbContext<ApplicationDbContext>(
            options => options
                .UseNpgsql(postgresOptions.ConnectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, DatabaseSchemas.Default))
                .UseSnakeCaseNamingConvention());
        
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }
    
    private static IServiceCollection AddRedisConfiguration(this IServiceCollection services, IConfiguration configuration)
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

    private static IServiceCollection AddCacheServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<MemoryCacheService>();
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<IDistributedCacheService, RedisCacheService>();

        return services;
    }
    
    public static void AddSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));
    }
}