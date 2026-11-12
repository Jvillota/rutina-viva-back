namespace RutaViva.Application.Abstractions.Messaging;

/// <summary>A domain event read from the outbox, ready to be published.</summary>
public sealed record OutboxEnvelope(Guid MessageId, string Type, string Payload, DateTimeOffset OccurredAt);

/// <summary>
/// Port to publish events outside the transaction that produced them. Today an adapter writes them to the client
/// inbox; replacing it with a message broker is a new adapter, without touching Domain or Application.
/// Implementations must be idempotent by <see cref="OutboxEnvelope.MessageId"/> (at-least-once delivery).
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken);
}
