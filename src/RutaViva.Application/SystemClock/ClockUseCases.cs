using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.Common;

namespace RutaViva.Application.SystemClock;

public sealed record ClockView(DateTimeOffset Now, DateOnly Today, int OffsetDays, bool DemoEnabled);

public sealed record GetClockRequest;

public sealed class GetClockUseCase(IDemoClock demoClock, IBusinessClock clock) : IUseCase<GetClockRequest, ClockView>
{
    public async Task<Result<ClockView>> ExecuteAsync(GetClockRequest request, CancellationToken cancellationToken = default)
    {
        if (!demoClock.IsEnabled)
        {
            return ApplicationErrors.DemoDisabled;
        }

        var offset = await demoClock.GetOffsetAsync(cancellationToken).ConfigureAwait(false);
        return new ClockView(clock.Now, clock.Today, (int)offset.TotalDays, demoClock.IsEnabled);
    }
}

public sealed record AdvanceClockRequest(int Days);

/// <summary>Demo only: moves the business clock forward (never back) for the API and the Worker at once.</summary>
public sealed class AdvanceClockUseCase(IDemoClock demoClock, IBusinessClock clock) : IUseCase<AdvanceClockRequest, ClockView>
{
    public async Task<Result<ClockView>> ExecuteAsync(AdvanceClockRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!demoClock.IsEnabled)
        {
            return ApplicationErrors.DemoDisabled;
        }

        if (request.Days <= 0)
        {
            return ApplicationErrors.AdvanceMustBePositive;
        }

        var offset = await demoClock.AdvanceAsync(TimeSpan.FromDays(request.Days), cancellationToken).ConfigureAwait(false);
        return new ClockView(clock.Now, clock.Today, (int)offset.TotalDays, demoClock.IsEnabled);
    }
}
