using CleanArchitecture.Application.Abstractions.Outbox;

namespace CleanArchitecture.Infrastructure.Outbox;

public sealed class OutboxSignal : IOutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0);

    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }

    public async Task WaitForSignalAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
    }
}