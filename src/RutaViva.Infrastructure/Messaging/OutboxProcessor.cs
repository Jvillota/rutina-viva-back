using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RutaViva.Application.Abstractions.Messaging;
using RutaViva.Infrastructure.Persistence;

namespace RutaViva.Infrastructure.Messaging;

/// <summary>
/// Reads a batch of pending outbox messages with FOR UPDATE SKIP LOCKED (several workers never take the same
/// message), publishes them and marks them as processed. Failures are counted and retried later.
/// </summary>
internal sealed partial class OutboxProcessor(
    RutaVivaDbContext db,
    IEventPublisher publisher,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    private const int MaxErrorLength = 2000;

    public async Task<OutboxBatchResult> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var messages = await db.OutboxMessages
            .FromSql(
                $"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND attempts < {OutboxMessage.MaxAttempts}
                ORDER BY occurred_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var (published, failed) = (0, 0);
        foreach (var message in messages)
        {
            // A failed SQL statement aborts the whole PostgreSQL transaction: isolate each message in a savepoint.
            await transaction.CreateSavepointAsync("outbox_message", cancellationToken).ConfigureAwait(false);
            try
            {
                await publisher.PublishAsync(new OutboxEnvelope(message.Id, message.Type, message.Payload, message.OccurredAt), cancellationToken)
                    .ConfigureAwait(false);
                message.ProcessedAt = timeProvider.GetUtcNow();
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackToSavepointAsync("outbox_message", cancellationToken).ConfigureAwait(false);
                message.Attempts++;
                message.LastError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
                failed++;
                LogPublishFailed(logger, message.Id, message.Type, message.Attempts, ex);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new OutboxBatchResult(messages.Count, published, failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({Type}) failed on attempt {Attempt}")]
    private static partial void LogPublishFailed(ILogger logger, Guid messageId, string type, int attempt, Exception exception);
}
