using Microsoft.Extensions.Logging;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Domain.Common;

namespace RutaViva.Application.Common;

/// <summary>Another transaction changed the same data first (optimistic concurrency or unique open-order index).</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Retries a whole load-change-save operation when it loses a concurrency race. Each retry starts from a clean
/// unit of work, so it re-reads the state written by the winner (decision D7).
/// </summary>
public sealed partial class ConcurrencyRetryExecutor(IUnitOfWork unitOfWork, ILogger<ConcurrencyRetryExecutor> logger)
{
    public const int MaxAttempts = 5;

    public static readonly Error RetriesExhausted =
        Error.Conflict("concurrency.retries_exhausted", "The data changed concurrently too many times. Please retry.");

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (ConcurrencyConflictException ex)
            {
                unitOfWork.Reset();
                LogConflict(logger, attempt, MaxAttempts, ex.Message);

                // Small randomized backoff so concurrent writers of the same aggregate stop colliding.
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 25) * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return RetriesExhausted;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrency conflict on attempt {Attempt}/{MaxAttempts}: {Reason}")]
    private static partial void LogConflict(ILogger logger, int attempt, int maxAttempts, string reason);
}
