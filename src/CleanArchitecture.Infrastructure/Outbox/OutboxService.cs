using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Shared;

namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxService(
    ApplicationDbContext dbContext,
    OutboxEventTypeRegistry registry) : IOutboxService
{
    public async Task AddAsync(IIntegrationEvent message, CancellationToken cancellationToken = default)
    {
        var eventType = registry.GetName(message.GetType());

        var content = JsonSerializer.Serialize(message, message.GetType(), OutboxJsonOptions.Default);

        var outboxMessage = new OutboxMessage(eventType, content);

        await dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }
}