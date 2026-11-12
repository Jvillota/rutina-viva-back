using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RutaViva.Application.Fleet;
using RutaViva.Application.ServiceOrders;
using RutaViva.Application.Telemetry;
using RutaViva.Domain.Vehicles;
using RutaViva.Infrastructure.Persistence;

namespace RutaViva.Test.IntegrationTest;

[Collection(ApiCollection.Name)]
public sealed class TelemetryApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reading_RepeatedOrOutOfOrder_IsIgnoredWithoutError()
    {
        var vehicleId = await _client.CreateVehicleWithOilChangeAsync(58_000);
        var url = $"/api/v1/vehicles/{vehicleId}/odometer-readings";

        var applied = await (await _client.PostJsonAsync(url, new { km = 59_200 })).ReadAsync<OdometerReadingResult>();
        var repeated = await (await _client.PostJsonAsync(url, new { km = 59_200 })).ReadAsync<OdometerReadingResult>();
        var late = await (await _client.PostJsonAsync(url, new { km = 58_900 })).ReadAsync<OdometerReadingResult>();

        applied.Should().Be(new OdometerReadingResult(OdometerReadingOutcome.Applied, 59_200, 0));
        repeated.Status.Should().Be(OdometerReadingOutcome.Ignored);
        late.Should().Be(new OdometerReadingResult(OdometerReadingOutcome.Ignored, 59_200, 0));

        var history = await (await _client.GetAsync(url)).ReadAsync<List<RutaViva.Application.Abstractions.Persistence.OdometerReadingEntry>>();
        history.Select(h => h.Outcome).Should().Equal(OdometerReadingOutcome.Ignored, OdometerReadingOutcome.Ignored, OdometerReadingOutcome.Applied);
    }

    [Fact]
    public async Task Reading_UnknownVehicle_Returns404()
    {
        var response = await _client.PostJsonAsync($"/api/v1/vehicles/{Guid.NewGuid()}/odometer-readings", new { km = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"km\":-1}")]
    public async Task Reading_Invalid_Returns400(string body)
    {
        var vehicleId = await _client.CreateVehicleWithOilChangeAsync(58_000);

        var response = await _client.PostAsync(
            $"/api/v1/vehicles/{vehicleId}/odometer-readings",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ConcurrentBurst_KeepsMaxOdometerOneOpenOrderAndOneOverdueEvent()
    {
        // The routine is due at 60,000 km: 21 simultaneous readings between 60,000 and 60,400.
        var vehicleId = await _client.CreateVehicleWithOilChangeAsync(59_500);
        var url = $"/api/v1/vehicles/{vehicleId}/odometer-readings";

        var responses = await Task.WhenAll(Enumerable.Range(0, 21)
            .Select(i => _client.PostJsonAsync(url, new { km = 60_000 + (i * 20) })));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var vehicle = await (await _client.GetAsync($"/api/v1/vehicles/{vehicleId}")).ReadAsync<VehicleDetailView>();
        vehicle.OdometerKm.Should().Be(60_400);

        var orders = await (await _client.GetAsync($"/api/v1/service-orders?vehicleId={vehicleId}")).ReadAsync<List<ServiceOrderView>>();
        orders.Should().ContainSingle();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RutaVivaDbContext>();
        var overdueEvents = await db.OutboxMessages.AsNoTracking().Where(m => m.Type == "RoutineOverdue").ToListAsync();
        overdueEvents.Count(m => m.Payload.Contains(vehicleId.ToString(), StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }
}
