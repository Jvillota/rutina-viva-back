using RutaViva.Domain.Common;

namespace RutaViva.Domain.ServiceOrders;

/// <summary>One entry of the order history: who moved it, from which status to which, and when.</summary>
public sealed class ServiceOrderTransition
{
    private ServiceOrderTransition()
    {
    }

    internal ServiceOrderTransition(ServiceOrderStatus? fromStatus, ServiceOrderStatus toStatus, Actor actor, DateTimeOffset occurredAt)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Actor = actor;
        OccurredAt = occurredAt;
    }

    public long Id { get; private set; }

    public ServiceOrderStatus? FromStatus { get; private set; }

    public ServiceOrderStatus ToStatus { get; private set; }

    public Actor Actor { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
