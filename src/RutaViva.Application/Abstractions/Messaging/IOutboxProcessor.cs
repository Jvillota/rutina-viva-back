namespace RutaViva.Application.Abstractions.Messaging;

public sealed record OutboxBatchResult(int Read, int Published, int Failed);

/// <summary>Dispatches pending outbox messages through <see cref="IEventPublisher"/> (at-least-once).</summary>
public interface IOutboxProcessor
{
    Task<OutboxBatchResult> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken);
}
