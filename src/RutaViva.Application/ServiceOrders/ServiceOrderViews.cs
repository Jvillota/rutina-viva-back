using RutaViva.Domain.Common;
using RutaViva.Domain.ServiceOrders;

namespace RutaViva.Application.ServiceOrders;

public sealed record ServiceOrderTransitionView(ServiceOrderStatus? From, ServiceOrderStatus To, Actor Actor, DateTimeOffset OccurredAt);

public sealed record ServiceOrderView(
    Guid Id,
    Guid VehicleId,
    string Plate,
    Guid RoutineId,
    string RoutineName,
    Guid WorkshopId,
    ServiceOrderStatus Status,
    decimal? QuoteAmountCop,
    ApprovalType? ApprovalType,
    bool AwaitingClientApproval,
    CancellationReason? CancellationReason,
    int? ServiceKm,
    DateOnly? ServiceDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ServiceOrderTransitionView> Transitions);

internal static class ServiceOrderMapper
{
    public static ServiceOrderView ToView(this ServiceOrder order) =>
        new(
            order.Id,
            order.VehicleId,
            order.VehiclePlate,
            order.RoutineId,
            order.RoutineName,
            order.WorkshopId,
            order.Status,
            order.QuoteAmountCop,
            order.ApprovalType,
            order.AwaitingClientApproval,
            order.CancellationReason,
            order.ServiceKm,
            order.ServiceDate,
            order.CreatedAt,
            order.UpdatedAt,
            order.Transitions
                .OrderBy(t => t.OccurredAt)
                .ThenBy(t => t.Id)
                .Select(t => new ServiceOrderTransitionView(t.FromStatus, t.ToStatus, t.Actor, t.OccurredAt))
                .ToList());
}
