using RutaViva.Domain.Common;

namespace RutaViva.Domain.Vehicles.Events;

/// <summary>A routine entered the last 10 % of its interval. Raised at most once per cycle (rule 2).</summary>
public sealed record RoutineDueSoon(
    Guid VehicleId,
    string Plate,
    Guid RoutineId,
    string RoutineName,
    EvaluationAxis Axis,
    int? DueKm,
    DateOnly? DueDate,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>A routine reached its interval (rule 2). A service order is opened for it (rule 3).</summary>
public sealed record RoutineOverdue(
    Guid VehicleId,
    string Plate,
    Guid RoutineId,
    string RoutineName,
    EvaluationAxis Axis,
    int? DueKm,
    DateOnly? DueDate,
    DateTimeOffset OccurredAt) : IDomainEvent;
