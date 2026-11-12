namespace RutaViva.Domain.Vehicles;

public sealed record RoutineStatusChange(Guid RoutineId, RoutineStatus From, RoutineStatus To, EvaluationAxis Axis);
