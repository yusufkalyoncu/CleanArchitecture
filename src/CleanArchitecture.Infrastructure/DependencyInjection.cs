using System.Reflection;
using CleanArchitecture.Application.Abstractions.DomainEvents;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Application.Abstractions.Option;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Caching;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.DomainEvents;
using CleanArchitecture.Infrastructure.EventBus;
using CleanArchitecture.Infrastructure.Locking;
using CleanArchitecture.Infrastructure.Outbox;
using CleanArchitecture.Infrastructure.RateLimiting;
using CleanArchitecture.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrutor;
using Serilog;

namespace CleanArchitecture.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services
            .AddEventDispatcher()
            .AddAppOptions(configuration, typeof(PostgresOptions).Assembly)
            .AddEventBus()
            .AddOutboxServices()
            .AddDatabase(configuration)
            .AddRedisConfiguration(configuration)
            .AddCacheServices()
            .AddLockManager()
            .AddAuthenticationInternal(configuration)
            .AddRateLimiting(configuration);

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

    private static IServiceCollection AddEventDispatcher(this IServiceCollection services)
    {
        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        return services;
    }

    private static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        services.Scan(selector => selector
            .FromAssemblies(typeof(IEventBus).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IIntegrationEventHandler<>)), publicOnly: false)
            .UsingRegistrationStrategy(RegistrationStrategy.Append)
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        
        return services;
    }

    public static void AddSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));
    }
}