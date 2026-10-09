using System.Reflection;
using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.Outbox;

public static class OutboxExtensions
{
    public static IServiceCollection AddOutboxServices(this IServiceCollection services)
    {
        // ── Registry ─────────────────────────────────────────────────────────
        // Scans relevant assemblies on startup to register IIntegrationEvent types.
        services.AddSingleton(_ =>
        {
            var assemblies = new[]
            {
                typeof(IIntegrationEvent).Assembly,          // Shared
                typeof(IOutboxService).Assembly,             // Application
                Assembly.GetExecutingAssembly()              // Infrastructure
            };
            return new OutboxEventTypeRegistry(assemblies);
        });

        // ── Core services ─────────────────────────────────────────────────────
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddSingleton<IOutboxSignal, OutboxSignal>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<OutboxInsertInterceptor>();

        // ── Background services ───────────────────────────────────────────────
        services.AddHostedService<OutboxBackgroundService>();
        services.AddHostedService<OutboxCleanupBackgroundService>();

        // ── IMessageContext ──────────────────────────────────────────────────
        // Scoped context injected into handlers; populated by the dispatcher.
        services.AddScoped<MessageContext>();
        services.AddScoped<IMessageContext>(sp => sp.GetRequiredService<MessageContext>());

        return services;
    }
}