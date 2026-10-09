using CleanArchitecture.Infrastructure.Outbox;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Inbox;

internal sealed class InboxCleanupBackgroundService(
    NpgsqlDataSource dataSource,
    IOptions<OutboxOptions> options,
    ILogger<InboxCleanupBackgroundService> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deletedCount = await CleanupInboxMessagesAsync(stoppingToken);

                if (deletedCount > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Cleaned up {Count} old inbox messages.", deletedCount);
                }

                // Run cleanup once a day, or any suitable interval. Since Outbox defaults to running daily for its cleanup (assuming similar frequency), 1 hour is fine for inbox too.
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while cleaning up inbox messages.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // backoff
            }
        }
    }

    internal async Task<int> CleanupInboxMessagesAsync(CancellationToken ct)
    {
        const string sql = """
            DELETE FROM public.inbox_messages
            WHERE message_id IN (
                SELECT message_id FROM public.inbox_messages
                WHERE processed_on_utc < now() - make_interval(days => @RetentionDays)
                LIMIT @BatchSize
            )
            """;

        var totalDeleted = 0;
        int deletedInBatch;

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        do
        {
            deletedInBatch = await connection.ExecuteAsync(sql, new
            {
                RetentionDays = _options.InboxRetentionDays,
                BatchSize = 1000 // Fixed batch size for cleanup
            });

            totalDeleted += deletedInBatch;

            // Small delay to prevent transaction log bloat / CPU spikes during heavy cleanup
            if (deletedInBatch > 0)
                await Task.Delay(50, ct);

        } while (deletedInBatch > 0 && !ct.IsCancellationRequested);

        return totalDeleted;
    }
}