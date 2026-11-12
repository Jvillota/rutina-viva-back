using RutaViva.Domain.Common;

namespace RutaViva.Domain.Vehicles;

/// <summary>
/// A preventive maintenance routine of one vehicle (oil change, brakes, tires...).
/// Lives inside the <see cref="Vehicle"/> aggregate because its kilometer evaluation needs the vehicle odometer.
/// </summary>
public sealed class MaintenanceRoutine : Entity
{
    public const int MaxNameLength = 100;

    private MaintenanceRoutine()
    {
        Name = null!;
        Interval = null!;
    }

    internal MaintenanceRoutine(Guid vehicleId, string name, MaintenanceInterval interval, int cycleStartKm, DateOnly cycleStartDate)
    {
        Id = Guid.CreateVersion7();
        VehicleId = vehicleId;
        Name = name;
        Interval = interval;
        CycleStartKm = cycleStartKm;
        CycleStartDate = cycleStartDate;
        Status = RoutineStatus.UpToDate;
    }

    public Guid VehicleId { get; private set; }

    public string Name { get; private set; }

    public MaintenanceInterval Interval { get; private set; }

    public int CycleStartKm { get; private set; }

    public DateOnly CycleStartDate { get; private set; }

    public RoutineStatus Status { get; private set; }

    /// <summary>When the «due soon» notice was raised in the current cycle; null if it was not raised.</summary>
    public DateTimeOffset? DueSoonNotifiedAt { get; private set; }

    public int? DueKm => Interval.DueKilometers(CycleStartKm);

    public DateOnly? DueDate => Interval.DueDate(CycleStartDate);

    public int? RemainingKm(int odometerKm) => DueKm - odometerKm;

    public int? RemainingDays(DateOnly today) => DueDate?.DayNumber - today.DayNumber;

    /// <summary>Kilometer axis, run on every applied odometer reading (rule 2).</summary>
    internal RoutineStatusChange? EvaluateByKilometers(int odometerKm)
    {
        if (DueKm is not int dueKm)
        {
            return null;
        }

        var remaining = dueKm - odometerKm;
        var target = remaining <= 0 ? RoutineStatus.Overdue
            : remaining <= Interval.KilometersThreshold() ? RoutineStatus.DueSoon
            : RoutineStatus.UpToDate;

        return Escalate(target, EvaluationAxis.Kilometers);
    }

    /// <summary>Time axis, run by the daily check (rule 2).</summary>
    internal RoutineStatusChange? EvaluateByTime(DateOnly today)
    {
        if (DueDate is not DateOnly dueDate)
        {
            return null;
        }

        var remaining = dueDate.DayNumber - today.DayNumber;
        var target = remaining <= 0 ? RoutineStatus.Overdue
            : remaining <= Interval.DaysThreshold(CycleStartDate) ? RoutineStatus.DueSoon
            : RoutineStatus.UpToDate;

        return Escalate(target, EvaluationAxis.Time);
    }

    internal void MarkDueSoonNotified(DateTimeOffset now) => DueSoonNotifiedAt = now;

    /// <summary>Starts a new cycle from the real service values reported by the workshop (rule 5).</summary>
    internal void Restart(int serviceKm, DateOnly serviceDate)
    {
        CycleStartKm = serviceKm;
        CycleStartDate = serviceDate;
        Status = RoutineStatus.UpToDate;
        DueSoonNotifiedAt = null;
    }

    /// <summary>
    /// Inside a cycle the status only moves forward (decision F): each axis can raise it, none can lower it.
    /// That is what makes «due with whichever happens first» hold without re-evaluating both axes together.
    /// </summary>
    private RoutineStatusChange? Escalate(RoutineStatus target, EvaluationAxis axis)
    {
        if (target <= Status)
        {
            return null;
        }

        var from = Status;
        Status = target;
        return new RoutineStatusChange(Id, from, target, axis);
    }
}
