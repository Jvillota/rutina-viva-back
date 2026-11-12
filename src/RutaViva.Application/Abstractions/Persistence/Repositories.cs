using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Application.Abstractions.Persistence;

/// <summary>Port for the <see cref="Vehicle"/> aggregate (with its routines).</summary>
public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken);

    Task<bool> PlateExistsAsync(string plate, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Vehicles the daily check must visit: with a time interval, or with an overdue routine (decision B).</summary>
    Task<IReadOnlyList<Guid>> ListIdsForDailyCheckAsync(CancellationToken cancellationToken);

    void Add(Vehicle vehicle);
}

/// <summary>Port for the <see cref="ServiceOrder"/> aggregate (with its history).</summary>
public interface IServiceOrderRepository
{
    Task<ServiceOrder?> GetByIdAsync(Guid serviceOrderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceOrder>> ListAsync(ServiceOrderStatus? status, Guid? vehicleId, CancellationToken cancellationToken);

    /// <summary>Routines of the vehicle that already have an open order (not completed nor cancelled).</summary>
    Task<IReadOnlySet<Guid>> GetRoutineIdsWithOpenOrderAsync(Guid vehicleId, CancellationToken cancellationToken);

    void Add(ServiceOrder serviceOrder);
}

public interface IWorkshopRepository
{
    Task<Workshop?> GetByIdAsync(Guid workshopId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Workshop>> ListAsync(CancellationToken cancellationToken);

    void Add(Workshop workshop);
}

/// <summary>
/// Atomic commit of everything tracked in the current scope, including the outbox messages of the domain events.
/// Throws <see cref="Common.ConcurrencyConflictException"/> when another transaction changed the same data.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Discards tracked state so a retry reloads fresh data.</summary>
    void Reset();
}
