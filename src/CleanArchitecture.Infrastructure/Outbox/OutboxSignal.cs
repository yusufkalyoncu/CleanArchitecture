using CleanArchitecture.Application.Abstractions.Outbox;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxSignal(IOptions<OutboxOptions> options) : IOutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly TimeSpan _pollingInterval = options.Value.PollingInterval;

    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }

    public async Task WaitForSignalAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(_pollingInterval, cancellationToken);
    }
}