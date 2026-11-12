using FluentAssertions;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Vehicles.Events;
using static RutaViva.Test.UnitTest.Domain.DomainFixtures;

namespace RutaViva.Test.UnitTest.Domain.Vehicles;

/// <summary>Time axis of rule 2 (daily check) and decision F (status only moves forward).</summary>
public class VehicleTimeEvaluationTests
{
    [Fact]
    public void EvaluateByTime_BeforeThreshold_KeepsUpToDate()
    {
        var (vehicle, routine) = VehicleWithOilChange();

        // Due 2026-07-01, threshold 18.1 days: 19 days left is still up to date.
        vehicle.EvaluateRoutinesByTime(new DateOnly(2026, 6, 12), Now).Should().BeEmpty();

        routine.Status.Should().Be(RoutineStatus.UpToDate);
    }

    [Fact]
    public void EvaluateByTime_WithinTenPercentOfRealDays_MovesToDueSoon()
    {
        var (vehicle, routine) = VehicleWithOilChange();

        // 18 days left <= 18.1 (10 % of 181 real days, decision C).
        var changes = vehicle.EvaluateRoutinesByTime(new DateOnly(2026, 6, 13), Now);

        changes.Should().ContainSingle().Which.Should().Be(
            new RoutineStatusChange(routine.Id, RoutineStatus.UpToDate, RoutineStatus.DueSoon, EvaluationAxis.Time));
        vehicle.DomainEvents.OfType<RoutineDueSoon>().Should().ContainSingle().Which.Axis.Should().Be(EvaluationAxis.Time);
    }

    [Fact]
    public void EvaluateByTime_OnDueDate_MovesToOverdue()
    {
        // Rule 1: with both intervals, time wins when the vehicle barely moves.
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 52_000);

        vehicle.EvaluateRoutinesByTime(new DateOnly(2026, 7, 1), Now);

        routine.Status.Should().Be(RoutineStatus.Overdue);
        vehicle.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RoutineOverdue>();
    }

    [Fact]
    public void EvaluateByTime_RunTwiceOnSameDate_IsIdempotent()
    {
        var (vehicle, _) = VehicleWithOilChange();
        var today = new DateOnly(2026, 6, 20);
        vehicle.EvaluateRoutinesByTime(today, Now);
        vehicle.ClearDomainEvents();

        var secondRun = vehicle.EvaluateRoutinesByTime(today, Now);

        secondRun.Should().BeEmpty();
        vehicle.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateByKm_WhenDueSoonByTime_DoesNotGoBackToUpToDate()
    {
        var (vehicle, routine) = VehicleWithOilChange();
        vehicle.EvaluateRoutinesByTime(new DateOnly(2026, 6, 20), Now);

        vehicle.RecordOdometerReading(51_000, Now);

        routine.Status.Should().Be(RoutineStatus.DueSoon);
    }

    [Fact]
    public void EvaluateByTime_WhenDueSoonByKm_DoesNotNotifyAgain()
    {
        var (vehicle, _) = VehicleWithOilChange();
        vehicle.RecordOdometerReading(59_100, Now);
        vehicle.ClearDomainEvents();

        vehicle.EvaluateRoutinesByTime(new DateOnly(2026, 6, 20), Now);

        vehicle.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateByTime_WhenRoutineHasOnlyKmInterval_DoesNotChangeIt()
    {
        var (vehicle, routine) = VehicleWithOilChange(km: 10_000, months: null);

        vehicle.EvaluateRoutinesByTime(new DateOnly(2030, 1, 1), Now);

        routine.Status.Should().Be(RoutineStatus.UpToDate);
    }
}
