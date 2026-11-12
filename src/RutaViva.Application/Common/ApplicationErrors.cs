using RutaViva.Domain.Common;

namespace RutaViva.Application.Common;

public static class ApplicationErrors
{
    public static Error VehicleNotFound(Guid vehicleId) =>
        Error.NotFound("vehicle.not_found", $"Vehicle '{vehicleId}' was not found.");

    public static Error WorkshopNotFound(Guid workshopId) =>
        Error.Validation("workshop.not_found", $"Workshop '{workshopId}' does not exist.");

    public static Error DuplicatePlate(string plate) =>
        Error.Conflict("vehicle.duplicate_plate", $"A vehicle with plate '{plate}' already exists.");

    public static Error NotificationNotFound(Guid notificationId) =>
        Error.NotFound("notification.not_found", $"Notification '{notificationId}' was not found.");

    public static readonly Error DemoDisabled =
        Error.NotFound("demo.disabled", "Demo endpoints are disabled.");

    public static readonly Error AdvanceMustBePositive =
        Error.Validation("clock.advance_not_positive", "The clock can only move forward by at least one day.");
}
