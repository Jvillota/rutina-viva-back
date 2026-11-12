using FluentAssertions;
using RutaViva.Domain.Common;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.ServiceOrders.Events;
using static RutaViva.Test.UnitTest.Domain.DomainFixtures;

namespace RutaViva.Test.UnitTest.Domain.ServiceOrders;

/// <summary>State machine of rule 4 and the completion data of rule 5.</summary>
public class ServiceOrderTests
{
    private static readonly QuoteApprovalPolicy Policy = new();
    private static readonly DateOnly Today = new(2026, 4, 5);

    [Fact]
    public void Open_ForOverdueRoutine_CreatesOrderWithAssignedWorkshop()
    {
        var order = NewOrder();

        order.Status.Should().Be(ServiceOrderStatus.Created);
        order.WorkshopId.Should().Be(WorkshopId);
        order.RoutineName.Should().Be("Oil change");
        order.IsOpen.Should().BeTrue();
        order.Transitions.Should().ContainSingle().Which.Actor.Should().Be(Actor.System);
        order.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ServiceOrderCreated>();
    }

    [Theory]
    [InlineData(800_000)]
    [InlineData(1_500_000)]
    public void SubmitQuote_AtOrBelowThreshold_IsApprovedAutomatically(decimal amount)
    {
        var order = NewOrder();

        order.SubmitQuote(Cop(amount), Policy, Now).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(ServiceOrderStatus.Approved);
        order.ApprovalType.Should().Be(ApprovalType.Automatic);
        order.Transitions.Select(t => t.ToStatus).Should().Equal(
            ServiceOrderStatus.Created, ServiceOrderStatus.Quoted, ServiceOrderStatus.Approved);
    }

    [Fact]
    public void SubmitQuote_AboveThreshold_WaitsForClient()
    {
        var order = NewOrder();

        order.SubmitQuote(Cop(1_500_001), Policy, Now);

        order.Status.Should().Be(ServiceOrderStatus.Quoted);
        order.AwaitingClientApproval.Should().BeTrue();
        order.DomainEvents.OfType<QuoteApprovalRequired>().Should().ContainSingle();
    }

    [Fact]
    public void SubmitQuote_WhenNotCreated_FailsWithConflict()
    {
        var order = NewOrder();
        order.SubmitQuote(Cop(800_000), Policy, Now);

        var result = order.SubmitQuote(Cop(900_000), Policy, Now);

        result.Error.Type.Should().Be(ErrorType.Conflict);
        order.QuoteAmountCop.Should().Be(800_000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Money_WhenNotPositive_FailsWithValidation(decimal amount)
    {
        Money.Create(amount).Error.Should().Be(ServiceOrderErrors.QuoteMustBePositive);
    }

    [Fact]
    public void Approve_QuotedOrder_IsManualApproval()
    {
        var order = QuotedAboveThreshold();

        order.Approve(Now).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(ServiceOrderStatus.Approved);
        order.ApprovalType.Should().Be(ApprovalType.Manual);
        order.Transitions.Last().Actor.Should().Be(Actor.Client);
    }

    [Fact]
    public void Approve_WhenNotQuoted_FailsWithConflict()
    {
        NewOrder().Approve(Now).Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Reject_QuotedOrder_CancelsIt()
    {
        var order = QuotedAboveThreshold();

        order.Reject(Now);

        order.Status.Should().Be(ServiceOrderStatus.Cancelled);
        order.CancellationReason.Should().Be(CancellationReason.QuoteRejected);
        order.IsOpen.Should().BeFalse();
        order.DomainEvents.OfType<ServiceOrderCancelled>().Should().ContainSingle();
    }

    [Fact]
    public void Cancel_BeforeExecution_IsAllowedInEveryEarlyStatus()
    {
        var created = NewOrder();
        var quoted = QuotedAboveThreshold();
        var approved = Approved();

        created.Cancel(Now).IsSuccess.Should().BeTrue();
        quoted.Cancel(Now).IsSuccess.Should().BeTrue();
        approved.Cancel(Now).IsSuccess.Should().BeTrue();
        approved.CancellationReason.Should().Be(CancellationReason.ClientCancelled);
    }

    [Fact]
    public void Cancel_InProgress_FailsWithConflict()
    {
        var order = InProgress();

        order.Cancel(Now).Error.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(ServiceOrderStatus.InProgress);
    }

    [Fact]
    public void Start_WhenNotApproved_FailsWithConflict()
    {
        NewOrder().Start(Now).Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Complete_InProgress_StoresRealServiceValues()
    {
        var order = InProgress();

        order.Complete(60_450, Today, Today, Now).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(ServiceOrderStatus.Completed);
        order.ServiceKm.Should().Be(60_450);
        order.ServiceDate.Should().Be(Today);
        order.IsOpen.Should().BeFalse();
        order.DomainEvents.OfType<ServiceOrderCompleted>().Should().ContainSingle();
    }

    [Fact]
    public void Complete_WhenApproved_FailsWithConflict()
    {
        Approved().Complete(60_450, Today, Today, Now).Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Complete_WithFutureDate_FailsWithValidation()
    {
        InProgress().Complete(60_450, Today.AddDays(1), Today, Now).Error.Should().Be(ServiceOrderErrors.ServiceDateInFuture);
    }

    [Fact]
    public void Complete_WithDateBeforeOrder_FailsWithValidation()
    {
        InProgress().Complete(60_450, Today.AddDays(-1), Today, Now).Error.Should().Be(ServiceOrderErrors.ServiceDateBeforeOrder);
    }

    [Fact]
    public void Complete_WithNegativeKm_FailsWithValidation()
    {
        InProgress().Complete(-1, Today, Today, Now).Error.Should().Be(ServiceOrderErrors.NegativeServiceKm);
    }

    private static Money Cop(decimal amount) => Money.Create(amount).Value;

    private static ServiceOrder NewOrder()
    {
        var (vehicle, routine) = VehicleWithOilChange(odometerKm: 60_000);
        return ServiceOrder.Open(vehicle, routine, Today, Now);
    }

    private static ServiceOrder QuotedAboveThreshold()
    {
        var order = NewOrder();
        order.SubmitQuote(Cop(2_000_000), Policy, Now);
        return order;
    }

    private static ServiceOrder Approved()
    {
        var order = QuotedAboveThreshold();
        order.Approve(Now);
        return order;
    }

    private static ServiceOrder InProgress()
    {
        var order = Approved();
        order.Start(Now);
        return order;
    }
}
