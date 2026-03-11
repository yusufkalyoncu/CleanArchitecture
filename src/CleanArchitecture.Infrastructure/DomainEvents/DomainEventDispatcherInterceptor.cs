using CleanArchitecture.Application.Abstractions.DomainEvents;
using CleanArchitecture.Shared;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanArchitecture.Infrastructure.DomainEvents;

public class DomainEventDispatcherInterceptor(
    IDomainEventsDispatcher domainEventsDispatcher) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null) return result;

        var aggregateRoots = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = aggregateRoots
            .SelectMany(x => x.DomainEvents)
            .ToList();

        if (domainEvents.Count == 0) return result;

        aggregateRoots.ForEach(x => x.ClearEvents());

        await domainEventsDispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result;
    }
}