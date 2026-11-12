using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.Common;
using RutaViva.Domain.Vehicles;

namespace RutaViva.Application.Telemetry;

public sealed record RegisterOdometerReadingRequest(Guid VehicleId, int Km);

public sealed record OdometerReadingResult(OdometerReadingOutcome Status, int CurrentOdometerKm, int ServiceOrdersOpened);

/// <summary>
/// Receives a reading from telemetry (or the simulator). Ignored readings change nothing but are logged.
/// Applied readings evaluate the kilometer axis and open orders for overdue routines (rules 2, 3 and 6),
/// all in one transaction, retried if a concurrent reading of the same vehicle wins the race.
/// </summary>
public sealed class RegisterOdometerReadingUseCase(
    IVehicleRepository vehicles,
    IOdometerReadingLog readingLog,
    OverdueRoutineOrderOpener orderOpener,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : IUseCase<RegisterOdometerReadingRequest, OdometerReadingResult>
{
    public Task<Result<OdometerReadingResult>> ExecuteAsync(RegisterOdometerReadingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return retry.ExecuteAsync(ct => RegisterAsync(request, ct), cancellationToken);
    }

    private async Task<Result<OdometerReadingResult>> RegisterAsync(RegisterOdometerReadingRequest request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetByIdAsync(request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return ApplicationErrors.VehicleNotFound(request.VehicleId);
        }

        var (today, now) = (clock.Today, clock.Now);
        var outcome = vehicle.RecordOdometerReading(request.Km, now);
        if (outcome.IsFailure)
        {
            return outcome.Error;
        }

        var opened = outcome.Value == OdometerReadingOutcome.Applied
            ? await orderOpener.OpenMissingOrdersAsync(vehicle, today, now, cancellationToken).ConfigureAwait(false)
            : 0;

        readingLog.Add(new OdometerReadingEntry(vehicle.Id, request.Km, outcome.Value, now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new OdometerReadingResult(outcome.Value, vehicle.OdometerKm, opened);
    }
}

public sealed record ListOdometerReadingsRequest(Guid VehicleId, int Take = 50);

public sealed class ListOdometerReadingsUseCase(IVehicleRepository vehicles, IOdometerReadingLog readingLog)
    : IUseCase<ListOdometerReadingsRequest, IReadOnlyList<OdometerReadingEntry>>
{
    public async Task<Result<IReadOnlyList<OdometerReadingEntry>>> ExecuteAsync(ListOdometerReadingsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await vehicles.GetByIdAsync(request.VehicleId, cancellationToken).ConfigureAwait(false) is null)
        {
            return ApplicationErrors.VehicleNotFound(request.VehicleId);
        }

        var take = Math.Clamp(request.Take, 1, 500);
        return Result.Success(await readingLog.ListAsync(request.VehicleId, take, cancellationToken).ConfigureAwait(false));
    }
}
