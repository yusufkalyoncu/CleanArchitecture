namespace CleanArchitecture.Infrastructure.Outbox;

internal interface IOutboxProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default);
}