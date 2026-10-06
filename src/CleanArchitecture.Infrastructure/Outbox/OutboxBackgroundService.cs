using CleanArchitecture.Application.Abstractions.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Outbox;

internal sealed class OutboxBackgroundService(
    IOutboxSignal signal,
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    private readonly int _batchSize = options.Value.BatchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox Processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                processed = await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown triggered.
                break;
            }
            catch (Exception ex)
            {
                // Catches all other exceptions (including non-shutdown OCEs like DB timeouts)
                // to prevent the BackgroundService from crashing.
                logger.LogError(ex, "Error in Outbox processing loop.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown occurred during error backoff.
                    break;
                }

                continue;
            }

            // Continue immediately if the batch was full.
            // Otherwise, wait for a signal or the polling timeout.
            if (processed >= _batchSize) continue;
            try
            {
                await signal.WaitForSignalAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Outbox Processor stopped.");
    }
}