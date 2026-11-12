using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Infrastructure.Persistence;

namespace RutaViva.Infrastructure.Time;

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Enables the adjustable clock and the demo endpoints. Never enable it in production.</summary>
    public bool Enabled { get; set; }

    /// <summary>Seeds workshops, vehicles and routines when the database is empty.</summary>
    public bool SeedData { get; set; }
}

/// <summary>
/// Real UTC time plus an offset stored in the database (decision D8). The offset is cached in memory and refreshed
/// by <see cref="DemoClockSynchronizer"/>, so the API and the Worker see the same business date.
/// </summary>
public sealed class DemoTimeProvider : TimeProvider
{
    private long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset;

    internal void SetOffset(TimeSpan offset) => Interlocked.Exchange(ref _offsetTicks, offset.Ticks);
}

internal sealed class DemoClock(RutaVivaDbContext db, DemoTimeProvider? timeProvider, Microsoft.Extensions.Options.IOptions<DemoOptions> options) : IDemoClock
{
    public bool IsEnabled => options.Value.Enabled && timeProvider is not null;

    public async Task<TimeSpan> GetOffsetAsync(CancellationToken cancellationToken)
    {
        var row = await db.SystemClock.AsNoTracking().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var offset = TimeSpan.FromSeconds(row?.OffsetSeconds ?? 0);
        timeProvider?.SetOffset(offset);
        return offset;
    }

    public async Task<TimeSpan> AdvanceAsync(TimeSpan by, CancellationToken cancellationToken)
    {
        var seconds = (long)by.TotalSeconds;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO system_clock (id, offset_seconds, updated_at) VALUES ({SystemClockRecord.SingletonId}, {seconds}, now())
            ON CONFLICT (id) DO UPDATE SET offset_seconds = system_clock.offset_seconds + EXCLUDED.offset_seconds, updated_at = now()
            """,
            cancellationToken).ConfigureAwait(false);

        return await GetOffsetAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Keeps the in-memory demo offset in sync with the database, for processes that did not advance it.</summary>
internal sealed partial class DemoClockSynchronizer(
    IServiceScopeFactory scopeFactory,
    ILogger<DemoClockSynchronizer> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IDemoClock>().GetOffsetAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The schema may not exist yet while the API applies migrations: just try again later.
                LogSyncFailed(logger, ex.Message);
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Demo clock sync skipped: {Reason}")]
    private static partial void LogSyncFailed(ILogger logger, string reason);
}
