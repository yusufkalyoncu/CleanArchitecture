using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Infrastructure.Inbox;

internal sealed class InboxHandlerDecorator<TEvent>(
    IIntegrationEventHandler<TEvent> inner,
    string consumer,
    ApplicationDbContext dbContext,
    IMessageContext messageContext,
    ILogger<InboxHandlerDecorator<TEvent>> logger) : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    public async Task Handle(TEvent @event, CancellationToken cancellationToken = default)
    {
        var messageId = messageContext.MessageId;

        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("InboxHandlerDecorator must be the outermost transaction owner.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO inbox_messages (consumer, message_id)
             VALUES ({consumer}, {messageId})
             ON CONFLICT DO NOTHING
             """,
            cancellationToken);

        if (affectedRows == 0)
        {
            logger.LogInformation("Duplicate message {MessageId} skipped for consumer {Consumer}.", messageId, consumer);
            return; // Implicitly disposed transaction causes rollback which is fine.
        }

        try
        {
            await inner.Handle(@event, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Handler {consumer} failed for message {messageId}.", ex);
        }

        await transaction.CommitAsync(CancellationToken.None);
    }
}