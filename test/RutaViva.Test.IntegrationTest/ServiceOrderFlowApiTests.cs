using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RutaViva.Application.Abstractions.Messaging;
using RutaViva.Application.Fleet;
using RutaViva.Application.ServiceOrders;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Infrastructure.Persistence;

namespace RutaViva.Test.IntegrationTest;

[Collection(ApiCollection.Name)]
public sealed class ServiceOrderFlowApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FullFlow_HighQuote_ClientApproves_WorkshopCompletes_RoutineRestarts()
    {
        var (vehicleId, orderId) = await VehicleWithOpenOrderAsync();

        var quoted = await Act(orderId, "quote", new { amountCop = 2_000_000 });
        quoted.Status.Should().Be(ServiceOrderStatus.Quoted);
        quoted.AwaitingClientApproval.Should().BeTrue();

        (await _client.PostJsonAsync($"/api/v1/service-orders/{orderId}/start")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await Act(orderId, "approve")).ApprovalType.Should().Be(ApprovalType.Manual);
        (await Act(orderId, "start")).Status.Should().Be(ServiceOrderStatus.InProgress);
        (await _client.PostJsonAsync($"/api/v1/service-orders/{orderId}/cancel")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var completed = await Act(orderId, "complete", new { serviceKm = 60_450, serviceDate = "2026-03-01" });

        completed.Status.Should().Be(ServiceOrderStatus.Completed);
        completed.Transitions.Select(t => t.To).Should().Equal(
            ServiceOrderStatus.Created, ServiceOrderStatus.Quoted, ServiceOrderStatus.Approved,
            ServiceOrderStatus.InProgress, ServiceOrderStatus.Completed);

        var vehicle = await (await _client.GetAsync($"/api/v1/vehicles/{vehicleId}")).ReadAsync<VehicleDetailView>();
        vehicle.OdometerKm.Should().Be(60_000, "the workshop km does not move the odometer (decision D)");
        vehicle.Routines.Should().ContainSingle().Which.Should().Match<RoutineView>(r =>
            r.Status == RoutineStatus.UpToDate && r.CycleStartKm == 60_450 && r.DueKm == 70_450 && r.OpenServiceOrderId == null);
    }

    [Fact]
    public async Task Quote_AtThreshold_IsApprovedAutomatically()
    {
        var (_, orderId) = await VehicleWithOpenOrderAsync();

        var order = await Act(orderId, "quote", new { amountCop = 1_500_000 });

        order.Status.Should().Be(ServiceOrderStatus.Approved);
        order.ApprovalType.Should().Be(ApprovalType.Automatic);
    }

    [Fact]
    public async Task RejectedQuote_ThenNewReading_OpensANewOrder()
    {
        // Decision B: after cancelling, the routine is still overdue and has no open order.
        var (vehicleId, orderId) = await VehicleWithOpenOrderAsync();
        await Act(orderId, "quote", new { amountCop = 3_000_000 });
        (await Act(orderId, "reject")).CancellationReason.Should().Be(CancellationReason.QuoteRejected);

        await _client.PostJsonAsync($"/api/v1/vehicles/{vehicleId}/odometer-readings", new { km = 60_100 });

        var orders = await (await _client.GetAsync($"/api/v1/service-orders?vehicleId={vehicleId}")).ReadAsync<List<ServiceOrderView>>();
        orders.Should().HaveCount(2);
        orders.Should().ContainSingle(o => o.Status == ServiceOrderStatus.Created);
    }

    [Fact]
    public async Task Database_RejectsASecondOpenOrderForTheSameRoutine()
    {
        var (vehicleId, _) = await VehicleWithOpenOrderAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RutaVivaDbContext>();
        var vehicle = await db.Vehicles.Include(v => v.Routines).SingleAsync(v => v.Id == vehicleId);

        db.ServiceOrders.Add(ServiceOrder.Open(vehicle, vehicle.Routines.Single(), new DateOnly(2026, 3, 1), DateTimeOffset.UtcNow));
        var save = () => db.SaveChangesAsync();

        (await save.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("ux_service_orders_open_per_routine");
    }

    [Fact]
    public async Task Outbox_IsPublishedToTheInbox_AndRetriesDoNotDuplicate()
    {
        await VehicleWithOpenOrderAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

        while ((await processor.ProcessBatchAsync(100, CancellationToken.None)).Read > 0)
        {
        }

        var db = scope.ServiceProvider.GetRequiredService<RutaVivaDbContext>();
        (await db.OutboxMessages.CountAsync(m => m.ProcessedAt == null)).Should().Be(0);
        var processed = await db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Type == "RoutineOverdue");
        var before = await db.Notifications.CountAsync();

        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        await publisher.PublishAsync(new OutboxEnvelope(processed.Id, processed.Type, processed.Payload, processed.OccurredAt), CancellationToken.None);

        (await db.Notifications.CountAsync()).Should().Be(before);
        (await db.Notifications.CountAsync(n => n.OutboxMessageId == processed.Id)).Should().Be(1);
    }

    private async Task<(Guid VehicleId, Guid OrderId)> VehicleWithOpenOrderAsync()
    {
        var vehicleId = await _client.CreateVehicleWithOilChangeAsync(59_500);
        await _client.PostJsonAsync($"/api/v1/vehicles/{vehicleId}/odometer-readings", new { km = 60_000 });
        var orders = await (await _client.GetAsync($"/api/v1/service-orders?vehicleId={vehicleId}")).ReadAsync<List<ServiceOrderView>>();
        return (vehicleId, orders.Single().Id);
    }

    private async Task<ServiceOrderView> Act(Guid orderId, string action, object? body = null) =>
        await (await _client.PostJsonAsync($"/api/v1/service-orders/{orderId}/{action}", body)).ReadAsync<ServiceOrderView>();
}
