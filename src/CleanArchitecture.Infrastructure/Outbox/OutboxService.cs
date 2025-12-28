using System.Text.Json;
using CleanArchitecture.Application.Abstractions.Outbox;
using CleanArchitecture.Infrastructure.Database;

namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxService(ApplicationDbContext dbContext) : IOutboxService
{
    public async Task AddAsync<T>(
        T message,
        CancellationToken cancellationToken = default) where T : class
    {
        var outboxMessage = new OutboxMessage(
            typeof(T).Name,
            JsonSerializer.Serialize(message));

        await dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        var count = dbContext.ChangeTracker.Entries<OutboxMessage>().Count();
    }
}