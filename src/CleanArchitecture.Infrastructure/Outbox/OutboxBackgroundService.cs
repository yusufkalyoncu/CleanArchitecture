using CleanArchitecture.Application.Abstractions.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Infrastructure.Outbox;

internal sealed class OutboxBackgroundService(
    IOutboxSignal signal,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox Processor Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                await outboxProcessor.ProcessBatchAsync(stoppingToken);

                await signal.WaitForSignalAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Outbox loop");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}