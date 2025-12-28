using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.EventBus;

public sealed class InMemoryEventBus(IServiceProvider serviceProvider) : IEventBus
{
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var eventType = integrationEvent.GetType();

        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        using var scope = serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler == null) continue;

            var method = handler.GetType().GetMethod("Handle");

            if (method == null) continue;

            var result = method.Invoke(handler, [integrationEvent, cancellationToken]);

            if (result is Task task)
            {
                await task;
            }
        }
    }
}