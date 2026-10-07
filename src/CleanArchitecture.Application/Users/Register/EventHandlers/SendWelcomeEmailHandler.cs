using CleanArchitecture.Application.Users.Register.Events;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.EventHandlers;

internal sealed class SendWelcomeEmailHandler(IMessageContext messageContext) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public Task Handle(
        UserRegisteredIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Design Note: This handler produces external side effects (Email).
        // Since database transactions cannot rollback an already-sent Email, we must pass an idempotency key to the external service.
        // The messageContext.MessageId + handler name ensures that if this handler runs again (e.g. timeout after Email sent, but before DB commit),
        // the external Email provider can reject the duplicate request.
        var idempotencyKey = $"{nameof(SendWelcomeEmailHandler)}-{messageContext.MessageId}";
        Console.WriteLine($"[IdempotencyKey: {idempotencyKey}] Welcome email sent to user with ID: {@event.UserId}");
        
        return Task.CompletedTask;
    }
}