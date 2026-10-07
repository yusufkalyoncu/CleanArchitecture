using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.EventHandlers;

internal sealed class SendWelcomeSmsHandler(IMessageContext messageContext) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public Task Handle(
        UserRegisteredIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Design Note: This handler produces external side effects (SMS).
        // Since database transactions cannot rollback an already-sent SMS, we must pass an idempotency key to the external service.
        // The messageContext.MessageId + handler name ensures that if this handler runs again (e.g. timeout after SMS sent, but before DB commit),
        // the external SMS provider can reject the duplicate request.
        var idempotencyKey = $"{nameof(SendWelcomeSmsHandler)}-{messageContext.MessageId}";
        Console.WriteLine($"[IdempotencyKey: {idempotencyKey}] Welcome sms sent to user with ID: {@event.UserId}");
        
        return Task.CompletedTask;
    }
}