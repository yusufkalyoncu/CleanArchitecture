namespace CleanArchitecture.Shared;

public interface IIntegrationEventHandler<in T> where T : IIntegrationEvent
{
    Task Handle(T @event, CancellationToken cancellationToken = default);
}