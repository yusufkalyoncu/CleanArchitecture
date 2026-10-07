using CleanArchitecture.Application.Abstractions.DomainEvents;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.Infrastructure.Caching;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.DomainEvents;
using CleanArchitecture.Infrastructure.EventBus;
using CleanArchitecture.Infrastructure.Inbox;
using CleanArchitecture.Infrastructure.Locking;
using CleanArchitecture.Infrastructure.Outbox;
using CleanArchitecture.Infrastructure.RateLimiting;
using CleanArchitecture.Shared;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            .AddInboxServices()
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

        var registry = new IntegrationEventHandlerRegistry();
        services.AddSingleton(registry);

        var handlerInterfaceOpenType = typeof(IIntegrationEventHandler<>);

        var handlerTypes = typeof(IEventBus).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceOpenType));

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);

            var handlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceOpenType);

            foreach (var handlerInterface in handlerInterfaces)
            {
                var eventType = handlerInterface.GetGenericArguments()[0];
                var decoratorType = typeof(InboxHandlerDecorator<>).MakeGenericType(eventType);

                services.AddKeyedScoped(handlerInterface, handlerType, (sp, _) =>
                    ActivatorUtilities.CreateInstance(
                        sp, decoratorType, sp.GetRequiredService(handlerType), handlerType.FullName!));

                registry.Register(eventType, handlerType);
            }
        }

        return services;
    }

    private static IServiceCollection AddInboxServices(this IServiceCollection services)
    {
        services.AddHostedService<InboxCleanupBackgroundService>();
        return services;
    }


    public static void AddSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));
    }
}