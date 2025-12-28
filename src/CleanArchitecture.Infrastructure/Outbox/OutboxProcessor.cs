using System.Collections.Concurrent;
using System.Text.Json;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Shared;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
    NpgsqlDataSource dataSource,
    IEventBus eventBus,
    ILogger<OutboxProcessor> logger)
{
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();
    private const int BatchSize = 100;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var messages = (await connection.QueryAsync<OutboxMessage>(
                """
                SELECT 
                    id AS Id, 
                    type AS Type, 
                    content AS Content 
                FROM outbox_messages
                WHERE processed_on_utc IS NULL
                ORDER BY occurred_on_utc
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
                """,
                new { BatchSize },
                transaction: transaction
            )).ToList();

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            var updateQueue = new ConcurrentQueue<OutboxUpdateResult>();

            await Task.WhenAll(messages.Select(msg => PublishMessageAsync(msg, updateQueue, cancellationToken)));

            if (!updateQueue.IsEmpty)
            {
                var results = updateQueue.ToArray();

                var sql = """
                          UPDATE outbox_messages AS m
                          SET processed_on_utc = u.processed_on_utc,
                              error = u.error
                          FROM (
                              SELECT * FROM UNNEST(@Ids, @Dates, @Errors) 
                              AS t(id, processed_on_utc, error) 
                          ) AS u
                          WHERE m.id = u.id
                          """;

                await connection.ExecuteAsync(sql, new
                {
                    Ids = results.Select(x => x.Id).ToArray(),
                    Dates = results.Select(x => x.ProcessedDate).ToArray(),
                    Errors = results.Select(x => x.Error).ToArray()
                }, transaction: transaction);
            }

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("{Count} messages processed.", messages.Count);
            return messages.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch processing failed.");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task PublishMessageAsync(
        OutboxMessage message,
        ConcurrentQueue<OutboxUpdateResult> resultQueue,
        CancellationToken ct)
    {
        string? error = null;
        try
        {
            var msgType = GetMessageType(message.Type);

            if (msgType != null)
            {
                var content = JsonSerializer.Deserialize(message.Content, msgType);

                if (content is IIntegrationEvent integrationEvent)
                {
                    await eventBus.PublishAsync(integrationEvent, ct);
                }
                else
                {
                    error = $"Content is null or not IIntegrationEvent. Type: {message.Type}";
                }
            }
            else
            {
                error = $"Type not found: {message.Type}";
            }
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }

        resultQueue.Enqueue(new OutboxUpdateResult(message.Id, DateTime.UtcNow, error));
    }

    private static Type? GetMessageType(string typeName)
    {
        return TypeCache.GetOrAdd(typeName, _ =>
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
        });
    }

    private readonly record struct OutboxUpdateResult(Guid Id, DateTime ProcessedDate, string? Error);
}