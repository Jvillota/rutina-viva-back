using FluentAssertions;
using RutaViva.Domain.ServiceOrders;
using static RutaViva.Test.UnitTest.Domain.DomainFixtures;

namespace RutaViva.Test.UnitTest.Domain.ServiceOrders;

public class PolicyTests
{
    [Theory]
    [InlineData(1_500_000, false)]
    [InlineData(1_500_001, true)]
    [InlineData(1, false)]
    public void QuoteApprovalPolicy_UsesStrictlyGreaterThanThreshold(decimal amount, bool requiresClient)
    {
        // Decision G: exactly 1,500,000 COP is approved automatically.
        new QuoteApprovalPolicy().RequiresClientApproval(Money.Create(amount).Value).Should().Be(requiresClient);
    }

    [Fact]
    public void QuoteApprovalPolicy_ThresholdIsConfigurable()
    {
        new QuoteApprovalPolicy(100).RequiresClientApproval(Money.Create(101).Value).Should().BeTrue();
    }

    [Fact]
    public void ShouldOpenOrder_OverdueWithoutOpenOrder_IsTrue()
    {
        var (vehicle, routine) = VehicleWithOilChange();
        vehicle.RecordOdometerReading(60_000, Now);

        MaintenanceOrderPolicy.ShouldOpenOrder(routine, hasOpenOrder: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldOpenOrder_OverdueWithOpenOrder_IsFalse()
    {
        var (vehicle, routine) = VehicleWithOilChange();
        vehicle.RecordOdometerReading(60_000, Now);

        MaintenanceOrderPolicy.ShouldOpenOrder(routine, hasOpenOrder: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldOpenOrder_NotOverdue_IsFalse()
    {
        var (_, routine) = VehicleWithOilChange();

        MaintenanceOrderPolicy.ShouldOpenOrder(routine, hasOpenOrder: false).Should().BeFalse();
    }
}
