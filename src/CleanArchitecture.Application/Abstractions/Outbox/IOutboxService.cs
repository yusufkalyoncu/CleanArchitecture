using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Abstractions.Outbox;

public interface IOutboxService
{
    Task AddAsync(IIntegrationEvent message, CancellationToken cancellationToken = default);
}