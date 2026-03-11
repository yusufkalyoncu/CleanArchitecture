using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.EventHandlers;

internal sealed class SendWelcomeSmsHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public Task Handle(
        UserRegisteredIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Welcome sms sent to user with ID: " + @event.UserId);
        return Task.CompletedTask;
    }
}