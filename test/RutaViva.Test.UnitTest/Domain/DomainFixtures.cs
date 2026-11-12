using RutaViva.Domain.Vehicles;

namespace RutaViva.Test.UnitTest.Domain;

/// <summary>Shared values from the challenge example: every 10,000 km or 6 months, cycle starting at 50,000 km on 2026-01-01.</summary>
internal static class DomainFixtures
{
    public static readonly DateOnly CycleStart = new(2026, 1, 1);
    public static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid WorkshopId = Guid.Parse("0192f1d2-0000-7000-8000-000000000001");

    public static MaintenanceInterval Interval(int? km = 10_000, int? months = 6) =>
        MaintenanceInterval.Create(km, months).Value;

    public static Vehicle VehicleAt(int odometerKm = 50_000) =>
        Vehicle.Register("ABC123", WorkshopId, odometerKm, Now).Value;

    /// <summary>A vehicle at <paramref name="odometerKm"/> with an «Oil change» routine whose cycle starts at 50,000 km / 2026-01-01.</summary>
    public static (Vehicle Vehicle, MaintenanceRoutine Routine) VehicleWithOilChange(
        int odometerKm = 50_000,
        int? km = 10_000,
        int? months = 6)
    {
        var vehicle = VehicleAt(odometerKm);
        var routine = vehicle.AddRoutine("Oil change", Interval(km, months), 50_000, CycleStart, CycleStart, Now).Value;
        vehicle.ClearDomainEvents();
        return (vehicle, routine);
    }
}
