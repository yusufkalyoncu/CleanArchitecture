using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Abstractions.DomainEvents;

public interface IDomainEventsDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}