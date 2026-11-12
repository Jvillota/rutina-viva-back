namespace RutaViva.Domain.ServiceOrders;

/// <summary>Lifecycle of a service order (rule 4). An order is open while it is neither completed nor cancelled.</summary>
public enum ServiceOrderStatus
{
    Created = 0,
    Quoted = 1,
    Approved = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
}

public enum ApprovalType
{
    /// <summary>Quote at or below the threshold: approved by the system.</summary>
    Automatic = 0,

    /// <summary>Quote above the threshold: approved by the client.</summary>
    Manual = 1,
}

public enum CancellationReason
{
    ClientCancelled = 0,
    QuoteRejected = 1,
}
