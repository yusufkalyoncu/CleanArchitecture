using CleanArchitecture.Application.Abstractions.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.Outbox;

public static class OutboxExtensions
{
    public static IServiceCollection AddOutboxServices(this IServiceCollection services)
    {
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddSingleton<IOutboxSignal, OutboxSignal>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();
        services.AddScoped<OutboxInsertInterceptor>();

        return services;
    }
}