using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;

namespace RutaViva.Application.Common;

/// <summary>
/// Applies rule 3 after any evaluation (reading, daily check, new routine): every overdue routine without an open
/// order gets one. The unique partial index in the database backs this up under concurrency.
/// </summary>
public sealed class OverdueRoutineOrderOpener(IServiceOrderRepository serviceOrders)
{
    public async Task<int> OpenMissingOrdersAsync(Vehicle vehicle, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        var overdue = vehicle.OverdueRoutines();
        if (overdue.Count == 0)
        {
            return 0;
        }

        var withOpenOrder = await serviceOrders.GetRoutineIdsWithOpenOrderAsync(vehicle.Id, cancellationToken).ConfigureAwait(false);
        var opened = 0;
        foreach (var routine in overdue)
        {
            if (MaintenanceOrderPolicy.ShouldOpenOrder(routine, withOpenOrder.Contains(routine.Id)))
            {
                serviceOrders.Add(ServiceOrder.Open(vehicle, routine, today, now));
                opened++;
            }
        }

        return opened;
    }
}
