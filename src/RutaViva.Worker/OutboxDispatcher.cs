using Microsoft.Extensions.Options;
using RutaViva.Application.Abstractions.Messaging;

namespace RutaViva.Worker;

/// <summary>Publishes pending domain events from the outbox. A thin host: the work lives in <see cref="IOutboxProcessor"/>.</summary>
internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerSettings> settings,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(settings.Value.OutboxPollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                var result = await processor.ProcessBatchAsync(settings.Value.OutboxBatchSize, stoppingToken);
                if (result.Read > 0)
                {
                    LogBatch(logger, result.Read, result.Published, result.Failed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Database not ready yet or transient failure: keep the loop alive and retry on the next tick.
                LogIterationFailed(logger, ex);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox batch: {Read} read, {Published} published, {Failed} failed")]
    private static partial void LogBatch(ILogger logger, int read, int published, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox dispatcher iteration failed")]
    private static partial void LogIterationFailed(ILogger logger, Exception exception);
}
