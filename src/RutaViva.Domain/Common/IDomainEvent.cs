namespace RutaViva.Domain.Common;

/// <summary>Something meaningful that happened in the domain. Persisted to the outbox with the change that caused it.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
