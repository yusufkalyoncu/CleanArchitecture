using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Abstractions.EventBus;

public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}