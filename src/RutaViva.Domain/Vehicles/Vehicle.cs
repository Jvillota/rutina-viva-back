using RutaViva.Domain.Common;
using RutaViva.Domain.Vehicles.Events;

namespace RutaViva.Domain.Vehicles;

/// <summary>
/// Aggregate root: a fleet vehicle, its odometer and its maintenance routines.
/// Enforces rule 6 (the odometer never decreases) and evaluates its routines (rule 2).
/// </summary>
public sealed class Vehicle : AggregateRoot
{
    public const int MaxPlateLength = 10;

    private readonly List<MaintenanceRoutine> _routines = [];

    private Vehicle()
    {
        Plate = null!;
    }

    public string Plate { get; private set; }

    /// <summary>Workshop already assigned to the vehicle. Choosing or scheduling it is out of scope.</summary>
    public Guid WorkshopId { get; private set; }

    public int OdometerKm { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<MaintenanceRoutine> Routines => _routines.AsReadOnly();

    public static Result<Vehicle> Register(string plate, Guid workshopId, int initialOdometerKm, DateTimeOffset now)
    {
        var normalizedPlate = plate?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedPlate.Length is 0 or > MaxPlateLength)
        {
            return VehicleErrors.PlateRequired;
        }

        if (workshopId == Guid.Empty)
        {
            return VehicleErrors.WorkshopRequired;
        }

        if (initialOdometerKm < 0)
        {
            return VehicleErrors.NegativeOdometer;
        }

        return new Vehicle
        {
            Id = Guid.CreateVersion7(),
            Plate = normalizedPlate,
            WorkshopId = workshopId,
            OdometerKm = initialOdometerKm,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Adds a routine. Its first cycle starts at the informed last service or, if none, at the current odometer and date.
    /// The routine is evaluated right away on both axes, so an already expired routine is detected at once.
    /// </summary>
    public Result<MaintenanceRoutine> AddRoutine(
        string name,
        MaintenanceInterval interval,
        int? lastServiceKm,
        DateOnly? lastServiceDate,
        DateOnly today,
        DateTimeOffset now)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is 0 or > MaintenanceRoutine.MaxNameLength)
        {
            return VehicleErrors.RoutineNameRequired;
        }

        if (_routines.Exists(r => string.Equals(r.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return VehicleErrors.DuplicateRoutineName(trimmedName);
        }

        if (lastServiceKm < 0)
        {
            return VehicleErrors.NegativeOdometer;
        }

        if (lastServiceKm > OdometerKm)
        {
            return VehicleErrors.LastServiceAfterOdometer;
        }

        if (lastServiceDate > today)
        {
            return VehicleErrors.LastServiceInFuture;
        }

        var routine = new MaintenanceRoutine(Id, trimmedName, interval, lastServiceKm ?? OdometerKm, lastServiceDate ?? today);
        _routines.Add(routine);

        Apply(routine.EvaluateByKilometers(OdometerKm), routine, now);
        Apply(routine.EvaluateByTime(today), routine, now);

        return routine;
    }

    /// <summary>
    /// Rule 6: a reading lower than or equal to the registered odometer is ignored without error.
    /// Because the odometer only grows, comparing kilometers is enough to discard both repeated and late readings.
    /// </summary>
    public Result<OdometerReadingOutcome> RecordOdometerReading(int km, DateTimeOffset now)
    {
        if (km < 0)
        {
            return VehicleErrors.NegativeOdometer;
        }

        if (km <= OdometerKm)
        {
            return OdometerReadingOutcome.Ignored;
        }

        OdometerKm = km;
        foreach (var routine in _routines)
        {
            Apply(routine.EvaluateByKilometers(km), routine, now);
        }

        return OdometerReadingOutcome.Applied;
    }

    /// <summary>Daily check: evaluates the time axis of every routine. Idempotent for the same date.</summary>
    public IReadOnlyList<RoutineStatusChange> EvaluateRoutinesByTime(DateOnly today, DateTimeOffset now)
    {
        var changes = new List<RoutineStatusChange>();
        foreach (var routine in _routines)
        {
            var change = routine.EvaluateByTime(today);
            Apply(change, routine, now);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    /// <summary>
    /// Rule 5: restarts the routine from the real service values. The odometer is NOT touched (decision D):
    /// it only changes with telemetry. The new cycle is evaluated right away against the current odometer and date.
    /// </summary>
    public Result RestartRoutine(Guid routineId, int serviceKm, DateOnly serviceDate, DateOnly today, DateTimeOffset now)
    {
        var routine = _routines.Find(r => r.Id == routineId);
        if (routine is null)
        {
            return VehicleErrors.RoutineNotFound(routineId);
        }

        routine.Restart(serviceKm, serviceDate);
        Apply(routine.EvaluateByKilometers(OdometerKm), routine, now);
        Apply(routine.EvaluateByTime(today), routine, now);
        return Result.Success();
    }

    public MaintenanceRoutine? FindRoutine(Guid routineId) => _routines.Find(r => r.Id == routineId);

    public IReadOnlyList<MaintenanceRoutine> OverdueRoutines() =>
        _routines.Where(r => r.Status == RoutineStatus.Overdue).ToList();

    /// <summary>
    /// Turns a status change into domain events. A jump straight from «up to date» to «overdue» raises only
    /// <see cref="RoutineOverdue"/> (decision A): the «due soon» moment never happened.
    /// </summary>
    private void Apply(RoutineStatusChange? change, MaintenanceRoutine routine, DateTimeOffset now)
    {
        switch (change?.To)
        {
            case RoutineStatus.DueSoon:
                routine.MarkDueSoonNotified(now);
                Raise(new RoutineDueSoon(Id, Plate, routine.Id, routine.Name, change.Axis, routine.DueKm, routine.DueDate, now));
                break;
            case RoutineStatus.Overdue:
                Raise(new RoutineOverdue(Id, Plate, routine.Id, routine.Name, change.Axis, routine.DueKm, routine.DueDate, now));
                break;
            default:
                break;
        }
    }
}
