using FluentAssertions;
using RutaViva.Domain.Common;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Vehicles.Events;
using static RutaViva.Test.UnitTest.Domain.DomainFixtures;

namespace RutaViva.Test.UnitTest.Domain.Vehicles;

/// <summary>Vehicle registration, adding routines (rule 1) and restarting a cycle (rule 5, decision D).</summary>
public class VehicleRoutineLifecycleTests
{
    [Fact]
    public void Register_WithValidData_NormalizesPlate()
    {
        var vehicle = Vehicle.Register(" abc123 ", WorkshopId, 45_000, Now).Value;

        vehicle.Plate.Should().Be("ABC123");
        vehicle.OdometerKm.Should().Be(45_000);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("ABCDEFGHIJK", 0)]
    public void Register_WithInvalidPlate_Fails(string plate, int km)
    {
        Vehicle.Register(plate, WorkshopId, km, Now).Error.Should().Be(VehicleErrors.PlateRequired);
    }

    [Fact]
    public void Register_WithNegativeOdometer_Fails()
    {
        Vehicle.Register("ABC123", WorkshopId, -1, Now).Error.Should().Be(VehicleErrors.NegativeOdometer);
    }

    [Fact]
    public void Register_WithoutWorkshop_Fails()
    {
        Vehicle.Register("ABC123", Guid.Empty, 0, Now).Error.Should().Be(VehicleErrors.WorkshopRequired);
    }

    [Fact]
    public void AddRoutine_WithoutLastService_StartsCycleAtCurrentOdometerAndDate()
    {
        var vehicle = VehicleAt(45_000);

        var routine = vehicle.AddRoutine("Oil change", Interval(), null, null, CycleStart, Now).Value;

        routine.Status.Should().Be(RoutineStatus.UpToDate);
        routine.CycleStartKm.Should().Be(45_000);
        routine.CycleStartDate.Should().Be(CycleStart);
        routine.DueKm.Should().Be(55_000);
    }

    [Fact]
    public void AddRoutine_WithExpiredLastService_IsEvaluatedRightAway()
    {
        var vehicle = VehicleAt(61_000);

        var routine = vehicle.AddRoutine("Oil change", Interval(), 50_000, CycleStart, CycleStart, Now).Value;

        routine.Status.Should().Be(RoutineStatus.Overdue);
        vehicle.DomainEvents.OfType<RoutineOverdue>().Should().ContainSingle();
    }

    [Fact]
    public void AddRoutine_WithDuplicateName_FailsWithConflict()
    {
        var (vehicle, _) = VehicleWithOilChange();

        var result = vehicle.AddRoutine("OIL CHANGE", Interval(), null, null, CycleStart, Now);

        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void AddRoutine_WithLastServiceAboveOdometer_Fails()
    {
        var vehicle = VehicleAt(40_000);

        vehicle.AddRoutine("Brakes", Interval(), 41_000, null, CycleStart, Now).Error
            .Should().Be(VehicleErrors.LastServiceAfterOdometer);
    }

    [Fact]
    public void AddRoutine_WithLastServiceInFuture_Fails()
    {
        var vehicle = VehicleAt();

        vehicle.AddRoutine("Brakes", Interval(), null, CycleStart.AddDays(1), CycleStart, Now).Error
            .Should().Be(VehicleErrors.LastServiceInFuture);
    }

    [Fact]
    public void RestartRoutine_FromWorkshopValues_StartsANewCycleUpToDate()
    {
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 60_300);
        vehicle.RecordOdometerReading(60_400, Now);

        var result = vehicle.RestartRoutine(routine.Id, 60_450, new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 5), Now);

        result.IsSuccess.Should().BeTrue();
        routine.Status.Should().Be(RoutineStatus.UpToDate);
        routine.CycleStartKm.Should().Be(60_450);
        routine.DueKm.Should().Be(70_450);
        routine.DueSoonNotifiedAt.Should().BeNull();
    }

    [Fact]
    public void RestartRoutine_DoesNotChangeTheOdometer()
    {
        // Decision D: only telemetry moves the odometer.
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 60_300);

        vehicle.RestartRoutine(routine.Id, 60_450, new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 5), Now);

        vehicle.OdometerKm.Should().Be(60_300);
    }

    [Fact]
    public void RestartRoutine_NewCycle_NotifiesDueSoonAgain()
    {
        var (vehicle, routine) = VehicleWithOilChange();
        vehicle.RecordOdometerReading(59_500, Now);
        vehicle.RestartRoutine(routine.Id, 59_500, new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 5), Now);
        vehicle.ClearDomainEvents();

        vehicle.RecordOdometerReading(68_600, Now);

        vehicle.DomainEvents.OfType<RoutineDueSoon>().Should().ContainSingle();
    }

    [Fact]
    public void RestartRoutine_WithUnknownRoutine_FailsWithNotFound()
    {
        var vehicle = VehicleAt();

        vehicle.RestartRoutine(Guid.NewGuid(), 1, CycleStart, CycleStart, Now).Error.Type.Should().Be(ErrorType.NotFound);
    }
}
