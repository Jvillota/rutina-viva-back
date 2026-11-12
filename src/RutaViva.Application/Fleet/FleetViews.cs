using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Application.Fleet;

public sealed record WorkshopView(Guid Id, string Name);

public sealed record VehicleSummaryView(
    Guid Id,
    string Plate,
    Guid WorkshopId,
    string WorkshopName,
    int OdometerKm,
    int RoutinesUpToDate,
    int RoutinesDueSoon,
    int RoutinesOverdue,
    int OpenServiceOrders);

public sealed record RoutineView(
    Guid Id,
    string Name,
    int? IntervalKm,
    int? IntervalMonths,
    int CycleStartKm,
    DateOnly CycleStartDate,
    RoutineStatus Status,
    int? DueKm,
    DateOnly? DueDate,
    int? RemainingKm,
    int? RemainingDays,
    Guid? OpenServiceOrderId);

public sealed record VehicleDetailView(
    Guid Id,
    string Plate,
    Guid WorkshopId,
    string WorkshopName,
    int OdometerKm,
    IReadOnlyList<RoutineView> Routines);

internal static class FleetMapper
{
    public static WorkshopView ToView(this Workshop workshop) => new(workshop.Id, workshop.Name);

    public static VehicleDetailView ToDetailView(
        this Vehicle vehicle,
        string workshopName,
        IReadOnlyDictionary<Guid, Guid> openOrderByRoutine,
        DateOnly today) =>
        new(
            vehicle.Id,
            vehicle.Plate,
            vehicle.WorkshopId,
            workshopName,
            vehicle.OdometerKm,
            vehicle.Routines
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new RoutineView(
                    r.Id,
                    r.Name,
                    r.Interval.Kilometers,
                    r.Interval.Months,
                    r.CycleStartKm,
                    r.CycleStartDate,
                    r.Status,
                    r.DueKm,
                    r.DueDate,
                    r.RemainingKm(vehicle.OdometerKm),
                    r.RemainingDays(today),
                    openOrderByRoutine.TryGetValue(r.Id, out var orderId) ? orderId : null))
                .ToList());

    public static VehicleSummaryView ToSummaryView(this Vehicle vehicle, string workshopName, int openOrders) =>
        new(
            vehicle.Id,
            vehicle.Plate,
            vehicle.WorkshopId,
            workshopName,
            vehicle.OdometerKm,
            vehicle.Routines.Count(r => r.Status == RoutineStatus.UpToDate),
            vehicle.Routines.Count(r => r.Status == RoutineStatus.DueSoon),
            vehicle.Routines.Count(r => r.Status == RoutineStatus.Overdue),
            openOrders);

    public static Dictionary<Guid, Guid> OpenOrderByRoutine(this IEnumerable<ServiceOrder> orders) =>
        orders.Where(o => o.IsOpen).ToDictionary(o => o.RoutineId, o => o.Id);
}
