using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.Common;
using RutaViva.Domain.Vehicles;

namespace RutaViva.Application.Fleet;

public sealed record RegisterVehicleRequest(string Plate, Guid WorkshopId, int InitialOdometerKm);

public sealed class RegisterVehicleUseCase(
    IVehicleRepository vehicles,
    IWorkshopRepository workshops,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : IUseCase<RegisterVehicleRequest, VehicleDetailView>
{
    public async Task<Result<VehicleDetailView>> ExecuteAsync(RegisterVehicleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = Vehicle.Register(request.Plate, request.WorkshopId, request.InitialOdometerKm, clock.Now);
        if (vehicle.IsFailure)
        {
            return vehicle.Error;
        }

        var workshop = await workshops.GetByIdAsync(request.WorkshopId, cancellationToken).ConfigureAwait(false);
        if (workshop is null)
        {
            return ApplicationErrors.WorkshopNotFound(request.WorkshopId);
        }

        if (await vehicles.PlateExistsAsync(vehicle.Value.Plate, cancellationToken).ConfigureAwait(false))
        {
            return ApplicationErrors.DuplicatePlate(vehicle.Value.Plate);
        }

        vehicles.Add(vehicle.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vehicle.Value.ToDetailView(workshop.Name, new Dictionary<Guid, Guid>(), clock.Today);
    }
}

public sealed record AddRoutineRequest(
    Guid VehicleId,
    string Name,
    int? IntervalKm,
    int? IntervalMonths,
    int? LastServiceKm,
    DateOnly? LastServiceDate);

/// <summary>Adds a routine; if its informed last service is already expired, the order is opened right away (rule 3).</summary>
public sealed class AddRoutineUseCase(
    IVehicleRepository vehicles,
    OverdueRoutineOrderOpener orderOpener,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : IUseCase<AddRoutineRequest, RoutineView>
{
    public Task<Result<RoutineView>> ExecuteAsync(AddRoutineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return retry.ExecuteAsync(ct => AddAsync(request, ct), cancellationToken);
    }

    private async Task<Result<RoutineView>> AddAsync(AddRoutineRequest request, CancellationToken cancellationToken)
    {
        var interval = MaintenanceInterval.Create(request.IntervalKm, request.IntervalMonths);
        if (interval.IsFailure)
        {
            return interval.Error;
        }

        var vehicle = await vehicles.GetByIdAsync(request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return ApplicationErrors.VehicleNotFound(request.VehicleId);
        }

        var (today, now) = (clock.Today, clock.Now);
        var routine = vehicle.AddRoutine(request.Name, interval.Value, request.LastServiceKm, request.LastServiceDate, today, now);
        if (routine.IsFailure)
        {
            return routine.Error;
        }

        await orderOpener.OpenMissingOrdersAsync(vehicle, today, now, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var r = routine.Value;
        return new RoutineView(
            r.Id, r.Name, r.Interval.Kilometers, r.Interval.Months, r.CycleStartKm, r.CycleStartDate, r.Status,
            r.DueKm, r.DueDate, r.RemainingKm(vehicle.OdometerKm), r.RemainingDays(today), null);
    }
}

public sealed record ListVehiclesRequest;

public sealed class ListVehiclesUseCase(
    IVehicleRepository vehicles,
    IWorkshopRepository workshops,
    IServiceOrderRepository serviceOrders) : IUseCase<ListVehiclesRequest, IReadOnlyList<VehicleSummaryView>>
{
    public async Task<Result<IReadOnlyList<VehicleSummaryView>>> ExecuteAsync(ListVehiclesRequest request, CancellationToken cancellationToken = default)
    {
        var all = await vehicles.ListAsync(cancellationToken).ConfigureAwait(false);
        var workshopNames = (await workshops.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(w => w.Id, w => w.Name);
        var openOrders = (await serviceOrders.ListAsync(null, null, cancellationToken).ConfigureAwait(false))
            .Where(o => o.IsOpen)
            .GroupBy(o => o.VehicleId)
            .ToDictionary(g => g.Key, g => g.Count());

        var views = all
            .OrderBy(v => v.Plate, StringComparer.Ordinal)
            .Select(v => v.ToSummaryView(workshopNames.GetValueOrDefault(v.WorkshopId, "?"), openOrders.GetValueOrDefault(v.Id)))
            .ToList();
        return Result.Success<IReadOnlyList<VehicleSummaryView>>(views);
    }
}

public sealed record GetVehicleDetailRequest(Guid VehicleId);

public sealed class GetVehicleDetailUseCase(
    IVehicleRepository vehicles,
    IWorkshopRepository workshops,
    IServiceOrderRepository serviceOrders,
    IBusinessClock clock) : IUseCase<GetVehicleDetailRequest, VehicleDetailView>
{
    public async Task<Result<VehicleDetailView>> ExecuteAsync(GetVehicleDetailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vehicle = await vehicles.GetByIdAsync(request.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return ApplicationErrors.VehicleNotFound(request.VehicleId);
        }

        var workshop = await workshops.GetByIdAsync(vehicle.WorkshopId, cancellationToken).ConfigureAwait(false);
        var orders = await serviceOrders.ListAsync(null, vehicle.Id, cancellationToken).ConfigureAwait(false);
        return vehicle.ToDetailView(workshop?.Name ?? "?", orders.OpenOrderByRoutine(), clock.Today);
    }
}
