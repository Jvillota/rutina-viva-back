using FluentAssertions;
using RutaViva.Domain.Vehicles;

namespace RutaViva.Test.UnitTest.Domain.Vehicles;

public class MaintenanceIntervalTests
{
    [Fact]
    public void Create_WhenNoIntervalIsGiven_FailsWithValidation()
    {
        var result = MaintenanceInterval.Create(null, null);

        result.Error.Should().Be(VehicleErrors.IntervalRequired);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, 0)]
    [InlineData(-10, 6)]
    public void Create_WhenAnIntervalIsNotPositive_FailsWithValidation(int? km, int? months)
    {
        var result = MaintenanceInterval.Create(km, months);

        result.Error.Should().Be(VehicleErrors.IntervalMustBePositive);
    }

    [Theory]
    [InlineData(10_000, null)]
    [InlineData(null, 6)]
    [InlineData(10_000, 6)]
    public void Create_WhenAtLeastOneIntervalIsPositive_Succeeds(int? km, int? months)
    {
        MaintenanceInterval.Create(km, months).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Thresholds_ForTheChallengeExample_AreTenPercent()
    {
        var interval = MaintenanceInterval.Create(10_000, 6).Value;
        var start = new DateOnly(2026, 1, 1);

        interval.DueKilometers(50_000).Should().Be(60_000);
        interval.KilometersThreshold().Should().Be(1_000m);
        interval.DueDate(start).Should().Be(new DateOnly(2026, 7, 1));
        interval.DaysThreshold(start).Should().Be(18.1m, "1-jan to 1-jul has 181 real days (decision C)");
    }

    [Fact]
    public void DueDate_AtEndOfMonth_UsesCalendarMonths()
    {
        var interval = MaintenanceInterval.Create(null, 1).Value;

        interval.DueDate(new DateOnly(2026, 1, 31)).Should().Be(new DateOnly(2026, 2, 28));
    }
}
