using Microsoft.Extensions.Options;
using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Routines;

namespace RutaViva.Worker;

/// <summary>
/// Runs the daily check (rule 2, time axis) once per business date. Polling the date instead of waiting for
/// midnight covers both the real day change and jumps of the demo clock.
/// </summary>
internal sealed partial class DailyCheckScheduler(
    IServiceScopeFactory scopeFactory,
    IBusinessClock clock,
    IOptions<WorkerSettings> settings,
    ILogger<DailyCheckScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(settings.Value.DailyCheckPollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var provider = scope.ServiceProvider;
                var lastRun = await provider.GetRequiredService<IUseCase<GetLastDailyCheckRequest, DateOnly?>>()
                    .ExecuteAsync(new GetLastDailyCheckRequest(), stoppingToken);

                if (lastRun.Value is not DateOnly last || clock.Today > last)
                {
                    LogStarting(logger, clock.Today);
                    await provider.GetRequiredService<IUseCase<RunDailyCheckRequest, DailyCheckSummary>>()
                        .ExecuteAsync(new RunDailyCheckRequest(), stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogIterationFailed(logger, ex);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting daily check for business date {BusinessDate}")]
    private static partial void LogStarting(ILogger logger, DateOnly businessDate);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Daily check scheduler iteration failed")]
    private static partial void LogIterationFailed(ILogger logger, Exception exception);
}
