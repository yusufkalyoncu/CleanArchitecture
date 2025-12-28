namespace CleanArchitecture.Application.Abstractions.Outbox;

public interface IOutboxService
{
    Task AddAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}