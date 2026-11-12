using RutaViva.Domain.Common;

namespace RutaViva.Domain.Vehicles;

public static class VehicleErrors
{
    public static readonly Error IntervalRequired =
        Error.Validation("routine.interval_required", "A routine needs an interval in kilometers, in months, or both.");

    public static readonly Error IntervalMustBePositive =
        Error.Validation("routine.interval_not_positive", "Routine intervals must be greater than zero.");

    public static readonly Error PlateRequired =
        Error.Validation("vehicle.plate_required", $"The plate is required and must have at most {Vehicle.MaxPlateLength} characters.");

    public static readonly Error WorkshopRequired =
        Error.Validation("vehicle.workshop_required", "The vehicle needs an assigned workshop.");

    public static readonly Error NegativeOdometer =
        Error.Validation("vehicle.negative_odometer", "Odometer values cannot be negative.");

    public static readonly Error RoutineNameRequired =
        Error.Validation("routine.name_required", $"The routine name is required and must have at most {MaintenanceRoutine.MaxNameLength} characters.");

    public static readonly Error LastServiceAfterOdometer =
        Error.Validation("routine.last_service_after_odometer", "The last service kilometers cannot exceed the current odometer.");

    public static readonly Error LastServiceInFuture =
        Error.Validation("routine.last_service_in_future", "The last service date cannot be in the future.");

    public static Error DuplicateRoutineName(string name) =>
        Error.Conflict("routine.duplicate_name", $"The vehicle already has a routine named '{name}'.");

    public static Error RoutineNotFound(Guid routineId) =>
        Error.NotFound("routine.not_found", $"Routine '{routineId}' was not found on this vehicle.");
}
