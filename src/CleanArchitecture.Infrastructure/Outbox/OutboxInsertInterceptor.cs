using CleanArchitecture.Application.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanArchitecture.Infrastructure.Outbox;

internal sealed class OutboxInsertInterceptor(
    IOutboxSignal outboxSignal) : SaveChangesInterceptor
{
    private bool _hasNewOutboxMessages;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _hasNewOutboxMessages = eventData.Context.ChangeTracker
                .Entries<OutboxMessage>()
                .Any(e => e.State == EntityState.Added);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var saveResult = await base.SavedChangesAsync(eventData, result, cancellationToken);

        if (saveResult > 0 && _hasNewOutboxMessages)
        {
            // Note: Assumes implicit transactions.
            // If explicit transactions (BeginTransaction) are used in the future, move this notification 
            // to DbTransactionInterceptor.TransactionCommittedAsync to avoid race conditions.
            outboxSignal.Notify();
            _hasNewOutboxMessages = false;
        }

        return saveResult;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _hasNewOutboxMessages = false;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _hasNewOutboxMessages = false;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}