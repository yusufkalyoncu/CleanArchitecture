using CleanArchitecture.Application.Abstractions.DomainEvents;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.Infrastructure.Caching;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.DomainEvents;
using CleanArchitecture.Infrastructure.EventBus;
using CleanArchitecture.Infrastructure.Locking;
using CleanArchitecture.Infrastructure.Outbox;
using CleanArchitecture.Infrastructure.RateLimiting;
using CleanArchitecture.Shared;
using FluentValidation;
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
            .AddValidators()
            .AddEventBus()
            .AddOutboxServices()
            .AddDatabase(configuration)
            .AddRedisConfiguration(configuration)
            .AddCacheServices()
            .AddLockManager()
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .AddRateLimiting(configuration);

    private static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }

    private static IServiceCollection AddEventDispatcher(this IServiceCollection services)
    {
        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();
        services.AddScoped<DomainEventDispatcherInterceptor>();

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