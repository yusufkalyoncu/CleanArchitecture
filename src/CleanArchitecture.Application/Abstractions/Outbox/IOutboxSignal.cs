namespace CleanArchitecture.Application.Abstractions.Outbox;

public interface IOutboxSignal
{
    void Notify();
    Task WaitForSignalAsync(CancellationToken cancellationToken);
}