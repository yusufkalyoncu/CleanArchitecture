using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Domain.Users.Events;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.EventHandlers;

internal sealed class UserRegisteredDomainEventHandler(
    IOutboxService outboxService) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await outboxService.AddAsync(
            new UserRegisteredIntegrationEvent(domainEvent.UserId),
            cancellationToken);
    }
}