namespace RutaViva.Domain.Vehicles;

/// <summary>
/// Status of a maintenance routine within its current cycle (rule 2).
/// Values are ordered: inside a cycle the status only moves forward.
/// </summary>
public enum RoutineStatus
{
    UpToDate = 0,
    DueSoon = 1,
    Overdue = 2,
}
