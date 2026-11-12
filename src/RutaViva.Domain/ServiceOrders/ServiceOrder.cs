using RutaViva.Domain.Common;
using RutaViva.Domain.ServiceOrders.Events;
using RutaViva.Domain.Vehicles;

namespace RutaViva.Domain.ServiceOrders;

/// <summary>
/// Aggregate root: a service order covering exactly one overdue routine (rule 3), sent to the workshop already
/// assigned to the vehicle. Each method validates its source status, so the state machine of rule 4 lives here.
/// </summary>
public sealed class ServiceOrder : AggregateRoot
{
    private readonly List<ServiceOrderTransition> _transitions = [];

    private ServiceOrder()
    {
        VehiclePlate = null!;
        RoutineName = null!;
    }

    public Guid VehicleId { get; private set; }

    public Guid RoutineId { get; private set; }

    public Guid WorkshopId { get; private set; }

    /// <summary>Snapshot of the plate when the order was opened, as on any printed work order.</summary>
    public string VehiclePlate { get; private set; }

    /// <summary>Snapshot of the routine name when the order was opened.</summary>
    public string RoutineName { get; private set; }

    public ServiceOrderStatus Status { get; private set; }

    public decimal? QuoteAmountCop { get; private set; }

    public ApprovalType? ApprovalType { get; private set; }

    public CancellationReason? CancellationReason { get; private set; }

    public int? ServiceKm { get; private set; }

    public DateOnly? ServiceDate { get; private set; }

    /// <summary>Business date of creation, used to validate the reported service date.</summary>
    public DateOnly CreatedOn { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ServiceOrderTransition> Transitions => _transitions.AsReadOnly();

    public bool IsOpen => Status is not (ServiceOrderStatus.Completed or ServiceOrderStatus.Cancelled);

    /// <summary>A quote above the threshold waiting for the client's decision.</summary>
    public bool AwaitingClientApproval => Status == ServiceOrderStatus.Quoted;

    public static ServiceOrder Open(Vehicle vehicle, MaintenanceRoutine routine, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(routine);

        var order = new ServiceOrder
        {
            Id = Guid.CreateVersion7(),
            VehicleId = vehicle.Id,
            RoutineId = routine.Id,
            WorkshopId = vehicle.WorkshopId,
            VehiclePlate = vehicle.Plate,
            RoutineName = routine.Name,
            Status = ServiceOrderStatus.Created,
            CreatedOn = today,
            CreatedAt = now,
            UpdatedAt = now,
        };

        order._transitions.Add(new ServiceOrderTransition(null, ServiceOrderStatus.Created, Actor.System, now));
        order.Raise(new ServiceOrderCreated(order.Id, order.VehicleId, order.VehiclePlate, order.RoutineId, order.RoutineName, order.WorkshopId, now));
        return order;
    }

    /// <summary>
    /// The workshop quotes the order. At or below the threshold it is approved right away (both transitions are
    /// recorded so the history is complete); above it, it waits for the client.
    /// </summary>
    public Result SubmitQuote(Money quote, QuoteApprovalPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(policy);

        if (Status != ServiceOrderStatus.Created)
        {
            return ServiceOrderErrors.InvalidTransition(Status, "quote");
        }

        QuoteAmountCop = quote.Amount;
        MoveTo(ServiceOrderStatus.Quoted, Actor.Workshop, now);
        Raise(new QuoteSubmitted(Id, VehicleId, VehiclePlate, RoutineName, quote.Amount, now));

        if (policy.RequiresClientApproval(quote))
        {
            Raise(new QuoteApprovalRequired(Id, VehicleId, VehiclePlate, RoutineName, quote.Amount, policy.ThresholdCop, now));
            return Result.Success();
        }

        ApprovalType = ServiceOrders.ApprovalType.Automatic;
        MoveTo(ServiceOrderStatus.Approved, Actor.System, now);
        Raise(new ServiceOrderApproved(Id, VehicleId, VehiclePlate, RoutineName, ServiceOrders.ApprovalType.Automatic, now));
        return Result.Success();
    }

    public Result Approve(DateTimeOffset now)
    {
        if (Status != ServiceOrderStatus.Quoted)
        {
            return ServiceOrderErrors.InvalidTransition(Status, "approve");
        }

        ApprovalType = ServiceOrders.ApprovalType.Manual;
        MoveTo(ServiceOrderStatus.Approved, Actor.Client, now);
        Raise(new ServiceOrderApproved(Id, VehicleId, VehiclePlate, RoutineName, ServiceOrders.ApprovalType.Manual, now));
        return Result.Success();
    }

    /// <summary>Rule 4: rejecting the quote cancels the order.</summary>
    public Result Reject(DateTimeOffset now)
    {
        if (Status != ServiceOrderStatus.Quoted)
        {
            return ServiceOrderErrors.InvalidTransition(Status, "reject");
        }

        CancelAs(ServiceOrders.CancellationReason.QuoteRejected, now);
        return Result.Success();
    }

    /// <summary>Rule 4: the client can cancel in any status before «in progress».</summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (Status is not (ServiceOrderStatus.Created or ServiceOrderStatus.Quoted or ServiceOrderStatus.Approved))
        {
            return ServiceOrderErrors.InvalidTransition(Status, "cancel");
        }

        CancelAs(ServiceOrders.CancellationReason.ClientCancelled, now);
        return Result.Success();
    }

    public Result Start(DateTimeOffset now)
    {
        if (Status != ServiceOrderStatus.Approved)
        {
            return ServiceOrderErrors.InvalidTransition(Status, "start");
        }

        MoveTo(ServiceOrderStatus.InProgress, Actor.Workshop, now);
        Raise(new ServiceOrderStarted(Id, VehicleId, VehiclePlate, RoutineName, now));
        return Result.Success();
    }

    /// <summary>
    /// Rule 5: the workshop reports the real service kilometers and date. The caller restarts the routine
    /// from these values in the same unit of work.
    /// </summary>
    public Result Complete(int serviceKm, DateOnly serviceDate, DateOnly today, DateTimeOffset now)
    {
        if (Status != ServiceOrderStatus.InProgress)
        {
            return ServiceOrderErrors.InvalidTransition(Status, "complete");
        }

        if (serviceKm < 0)
        {
            return ServiceOrderErrors.NegativeServiceKm;
        }

        if (serviceDate > today)
        {
            return ServiceOrderErrors.ServiceDateInFuture;
        }

        if (serviceDate < CreatedOn)
        {
            return ServiceOrderErrors.ServiceDateBeforeOrder;
        }

        ServiceKm = serviceKm;
        ServiceDate = serviceDate;
        MoveTo(ServiceOrderStatus.Completed, Actor.Workshop, now);
        Raise(new ServiceOrderCompleted(Id, VehicleId, VehiclePlate, RoutineId, RoutineName, serviceKm, serviceDate, now));
        return Result.Success();
    }

    private void CancelAs(CancellationReason reason, DateTimeOffset now)
    {
        CancellationReason = reason;
        MoveTo(ServiceOrderStatus.Cancelled, Actor.Client, now);
        Raise(new ServiceOrderCancelled(Id, VehicleId, VehiclePlate, RoutineId, RoutineName, reason, now));
    }

    private void MoveTo(ServiceOrderStatus target, Actor actor, DateTimeOffset now)
    {
        _transitions.Add(new ServiceOrderTransition(Status, target, actor, now));
        Status = target;
        UpdatedAt = now;
    }
}
