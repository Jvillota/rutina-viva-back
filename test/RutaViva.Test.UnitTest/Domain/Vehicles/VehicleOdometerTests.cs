using FluentAssertions;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Vehicles.Events;
using static RutaViva.Test.UnitTest.Domain.DomainFixtures;

namespace RutaViva.Test.UnitTest.Domain.Vehicles;

/// <summary>Rule 6 and the kilometer axis of rule 2.</summary>
public class VehicleOdometerTests
{
    [Fact]
    public void RecordReading_WhenKmIsGreater_UpdatesOdometer()
    {
        var vehicle = VehicleAt(58_000);

        var outcome = vehicle.RecordOdometerReading(59_200, Now).Value;

        outcome.Should().Be(OdometerReadingOutcome.Applied);
        vehicle.OdometerKm.Should().Be(59_200);
    }

    [Fact]
    public void RecordReading_WhenKmIsEqual_IsIgnoredWithoutError()
    {
        var vehicle = VehicleAt(59_200);

        var result = vehicle.RecordOdometerReading(59_200, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(OdometerReadingOutcome.Ignored);
        vehicle.OdometerKm.Should().Be(59_200);
    }

    [Fact]
    public void RecordReading_WhenKmIsLower_IsIgnoredWithoutError()
    {
        var vehicle = VehicleAt(59_200);

        var result = vehicle.RecordOdometerReading(58_900, Now);

        result.Value.Should().Be(OdometerReadingOutcome.Ignored);
        vehicle.OdometerKm.Should().Be(59_200);
    }

    [Fact]
    public void RecordReading_WhenIgnored_RaisesNoDomainEventsNorChangesRoutines()
    {
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 59_500);
        var statusBefore = routine.Status;

        vehicle.RecordOdometerReading(59_000, Now);

        vehicle.DomainEvents.Should().BeEmpty();
        routine.Status.Should().Be(statusBefore);
    }

    [Fact]
    public void RecordReading_WhenKmIsNegative_FailsWithValidation()
    {
        var vehicle = VehicleAt();

        vehicle.RecordOdometerReading(-1, Now).Error.Should().Be(VehicleErrors.NegativeOdometer);
    }

    [Fact]
    public void RecordReading_UnorderedSequenceWithDuplicates_OnlyAppliesIncreasingValues()
    {
        var vehicle = VehicleAt(0);
        int[] readings = [58_000, 59_200, 59_200, 58_900, 60_100, 59_800, 60_100];

        var outcomes = readings.Select(km => vehicle.RecordOdometerReading(km, Now).Value).ToList();

        outcomes.Should().Equal(
            OdometerReadingOutcome.Applied,
            OdometerReadingOutcome.Applied,
            OdometerReadingOutcome.Ignored,
            OdometerReadingOutcome.Ignored,
            OdometerReadingOutcome.Applied,
            OdometerReadingOutcome.Ignored,
            OdometerReadingOutcome.Ignored);
        vehicle.OdometerKm.Should().Be(60_100);
    }

    [Fact]
    public void RecordReading_JustBeforeThreshold_KeepsRoutineUpToDate()
    {
        var (vehicle, routine) = VehicleWithOilChange();

        vehicle.RecordOdometerReading(58_999, Now);

        routine.Status.Should().Be(RoutineStatus.UpToDate);
        vehicle.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordReading_WhenEnteringThreshold_RaisesDueSoonOnce()
    {
        var (vehicle, routine) = VehicleWithOilChange();

        vehicle.RecordOdometerReading(59_000, Now);
        vehicle.RecordOdometerReading(59_500, Now);
        vehicle.RecordOdometerReading(59_900, Now);

        routine.Status.Should().Be(RoutineStatus.DueSoon);
        routine.DueSoonNotifiedAt.Should().Be(Now);
        vehicle.DomainEvents.OfType<RoutineDueSoon>().Should().ContainSingle()
            .Which.Should().Match<RoutineDueSoon>(e => e.Axis == EvaluationAxis.Kilometers && e.DueKm == 60_000);
    }

    [Fact]
    public void RecordReading_WhenIntervalIsReached_RaisesOverdue()
    {
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 59_500);
        vehicle.RecordOdometerReading(59_600, Now);

        vehicle.RecordOdometerReading(60_000, Now);

        routine.Status.Should().Be(RoutineStatus.Overdue);
        vehicle.DomainEvents.OfType<RoutineOverdue>().Should().ContainSingle();
    }

    [Fact]
    public void RecordReading_WhenJumpingStraightToOverdue_RaisesOverdueWithoutDueSoon()
    {
        // Decision A: the «due soon» moment never happened, so it is not notified.
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 58_500);

        vehicle.RecordOdometerReading(61_000, Now);

        routine.Status.Should().Be(RoutineStatus.Overdue);
        vehicle.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoutineOverdue>();
    }

    [Fact]
    public void RecordReading_WhenAlreadyOverdue_DoesNotRaiseAgain()
    {
        var (vehicle, _) = VehicleWithOilChange();
        vehicle.RecordOdometerReading(60_000, Now);
        vehicle.ClearDomainEvents();

        vehicle.RecordOdometerReading(60_500, Now);

        vehicle.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordReading_WhenRoutineHasOnlyTimeInterval_DoesNotChangeIt()
    {
        var (vehicle, routine) = VehicleWithOilChange(km: null, months: 6);

        vehicle.RecordOdometerReading(500_000, Now);

        routine.Status.Should().Be(RoutineStatus.UpToDate);
    }
}
