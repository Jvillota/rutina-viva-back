using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RutaViva.Domain.Common;

namespace RutaViva.Infrastructure.Persistence;

/// <summary>
/// Transactional outbox: right before saving, turns the domain events of every tracked aggregate into
/// <see cref="OutboxMessage"/> rows, so they are committed in the same transaction as the change (decision D6).
/// </summary>
public sealed class DomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        MoveEventsToOutbox(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        MoveEventsToOutbox(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void MoveEventsToOutbox(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = Guid.CreateVersion7(),
                    Type = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    OccurredAt = domainEvent.OccurredAt,
                });
            }

            aggregate.ClearDomainEvents();
        }
    }
}
