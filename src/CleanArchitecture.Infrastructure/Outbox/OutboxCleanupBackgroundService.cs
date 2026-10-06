using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Outbox;

/// <summary>
/// Periodically deletes processed and dead-lettered outbox messages older than RetentionDays.
/// Deletes in batches to avoid locking the table.
/// </summary>
internal sealed class OutboxCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxCleanupBackgroundService> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;
    private const int CleanupBatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox Cleanup started (retention={RetentionDays} days).", _options.RetentionDays);

        // Runs once a day.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during Outbox cleanup.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Outbox Cleanup stopped.");
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        int totalDeleted = 0;

        // Delete in batches to avoid long table locks.
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            await using var connection = await dataSource.OpenConnectionAsync(ct);

            const string sql = """
                DELETE FROM public.outbox_messages
                WHERE id IN (
                    SELECT id FROM public.outbox_messages
                    WHERE (
                        (processed_on_utc IS NOT NULL AND processed_on_utc < @Cutoff)
                        OR
                        (dead_lettered_on_utc IS NOT NULL AND dead_lettered_on_utc < @Cutoff)
                    )
                    LIMIT @BatchSize
                )
                """;

            var deleted = await connection.ExecuteAsync(sql, new { Cutoff = cutoff, BatchSize = CleanupBatchSize });
            totalDeleted += deleted;

            if (deleted < CleanupBatchSize)
                break;

            // Brief pause between batches to reduce database pressure.
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        if (totalDeleted > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Outbox cleanup deleted {Count} old messages (cutoff={Cutoff:u}).",
                totalDeleted, cutoff);
        }
    }
}