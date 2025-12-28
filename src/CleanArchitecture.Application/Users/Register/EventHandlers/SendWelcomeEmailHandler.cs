using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.EventHandlers;

internal sealed class SendWelcomeEmailHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public Task Handle(
        UserRegisteredIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Welcome email sent to user with ID: " + @event.UserId);
        return Task.CompletedTask;
    }
}