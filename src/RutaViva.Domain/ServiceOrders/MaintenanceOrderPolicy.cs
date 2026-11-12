using RutaViva.Domain.Vehicles;

namespace RutaViva.Domain.ServiceOrders;

/// <summary>
/// Rule 3: an overdue routine gets a service order, and there is at most one open order per vehicle and routine.
/// After a cancellation the routine is still overdue and has no open order, so the next evaluation opens a new one
/// (decision B). Keeping the rule in one place makes it trivial to change if the business decides otherwise.
/// </summary>
public static class MaintenanceOrderPolicy
{
    public static bool ShouldOpenOrder(MaintenanceRoutine routine, bool hasOpenOrder) =>
        routine.Status == RoutineStatus.Overdue && !hasOpenOrder;
}
