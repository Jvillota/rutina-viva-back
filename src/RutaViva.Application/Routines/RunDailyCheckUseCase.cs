using Microsoft.Extensions.Logging;
using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.Common;

namespace RutaViva.Application.Routines;

public sealed record RunDailyCheckRequest;

public sealed record DailyCheckSummary(DateOnly BusinessDate, int VehiclesChecked, int RoutinesChanged, int ServiceOrdersOpened);

/// <summary>
/// Daily check (rule 2, time axis): evaluates every routine with a time interval and opens orders for overdue
/// routines without an open one (rule 3, decision B). Each vehicle is processed in its own transaction so one
/// failure does not stop the rest. Idempotent: running it twice the same day changes nothing the second time.
/// </summary>
public sealed partial class RunDailyCheckUseCase(
    IVehicleRepository vehicles,
    OverdueRoutineOrderOpener orderOpener,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IDailyCheckRunRepository runs,
    IBusinessClock clock,
    ILogger<RunDailyCheckUseCase> logger) : IUseCase<RunDailyCheckRequest, DailyCheckSummary>
{
    public async Task<Result<DailyCheckSummary>> ExecuteAsync(RunDailyCheckRequest request, CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var startedAt = clock.Now;
        var vehicleIds = await vehicles.ListIdsForDailyCheckAsync(cancellationToken).ConfigureAwait(false);

        var (routinesChanged, ordersOpened) = (0, 0);
        foreach (var vehicleId in vehicleIds)
        {
            var result = await retry.ExecuteAsync(ct => CheckVehicleAsync(vehicleId, today, ct), cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                routinesChanged += result.Value.RoutinesChanged;
                ordersOpened += result.Value.OrdersOpened;
            }
            else
            {
                LogVehicleSkipped(logger, vehicleId, result.Error.Code);
            }

            unitOfWork.Reset();
        }

        await runs.RecordAsync(new DailyCheckRun(today, startedAt, clock.Now, routinesChanged, ordersOpened), cancellationToken).ConfigureAwait(false);
        LogCompleted(logger, today, vehicleIds.Count, routinesChanged, ordersOpened);
        return new DailyCheckSummary(today, vehicleIds.Count, routinesChanged, ordersOpened);
    }

    private async Task<Result<(int RoutinesChanged, int OrdersOpened)>> CheckVehicleAsync(Guid vehicleId, DateOnly today, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetByIdAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return ApplicationErrors.VehicleNotFound(vehicleId);
        }

        var now = clock.Now;
        var changes = vehicle.EvaluateRoutinesByTime(today, now);
        var opened = await orderOpener.OpenMissingOrdersAsync(vehicle, today, now, cancellationToken).ConfigureAwait(false);
        if (changes.Count > 0 || opened > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (changes.Count, opened);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Daily check skipped vehicle {VehicleId}: {ErrorCode}")]
    private static partial void LogVehicleSkipped(ILogger logger, Guid vehicleId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Daily check {BusinessDate}: {Vehicles} vehicles, {RoutinesChanged} routines changed, {OrdersOpened} orders opened")]
    private static partial void LogCompleted(ILogger logger, DateOnly businessDate, int vehicles, int routinesChanged, int ordersOpened);
}

public sealed record GetLastDailyCheckRequest;

public sealed class GetLastDailyCheckUseCase(IDailyCheckRunRepository runs) : IUseCase<GetLastDailyCheckRequest, DateOnly?>
{
    public async Task<Result<DateOnly?>> ExecuteAsync(GetLastDailyCheckRequest request, CancellationToken cancellationToken = default) =>
        Result.Success(await runs.GetLastBusinessDateAsync(cancellationToken).ConfigureAwait(false));
}
