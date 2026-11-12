using RutaViva.Domain.Common;

namespace RutaViva.Domain.ServiceOrders.Events;

public sealed record ServiceOrderCreated(
    Guid ServiceOrderId,
    Guid VehicleId,
    string Plate,
    Guid RoutineId,
    string RoutineName,
    Guid WorkshopId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record QuoteSubmitted(Guid ServiceOrderId, Guid VehicleId, string Plate, string RoutineName, decimal AmountCop, DateTimeOffset OccurredAt)
    : IDomainEvent;

/// <summary>The quote is above the threshold: the client must approve or reject it (rule 4).</summary>
public sealed record QuoteApprovalRequired(
    Guid ServiceOrderId,
    Guid VehicleId,
    string Plate,
    string RoutineName,
    decimal AmountCop,
    decimal ThresholdCop,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ServiceOrderApproved(
    Guid ServiceOrderId,
    Guid VehicleId,
    string Plate,
    string RoutineName,
    ApprovalType ApprovalType,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ServiceOrderStarted(Guid ServiceOrderId, Guid VehicleId, string Plate, string RoutineName, DateTimeOffset OccurredAt)
    : IDomainEvent;

public sealed record ServiceOrderCompleted(
    Guid ServiceOrderId,
    Guid VehicleId,
    string Plate,
    Guid RoutineId,
    string RoutineName,
    int ServiceKm,
    DateOnly ServiceDate,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ServiceOrderCancelled(
    Guid ServiceOrderId,
    Guid VehicleId,
    string Plate,
    Guid RoutineId,
    string RoutineName,
    CancellationReason Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
