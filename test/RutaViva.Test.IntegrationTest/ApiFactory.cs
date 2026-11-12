using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace RutaViva.Test.IntegrationTest;

/// <summary>
/// Hosts the real API against a real PostgreSQL in a throwaway container (Testcontainers), with migrations applied
/// and a controllable clock. Requires Docker.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // 2026-03-01 12:00 in Bogota.
    public static readonly DateTimeOffset StartUtc = new(2026, 3, 1, 17, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("rutaviva_tests")
        .Build();

    public FakeTimeProvider Time { get; } = new(StartUtc);

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:RutaViva", _postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Demo:Enabled", "false");
        builder.UseSetting("Demo:SeedData", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

/// <summary>Small typed helpers over the HTTP API so tests read like the scenarios in the specs.</summary>
internal static class ApiClientExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, ApiFactory.Json);

    /// <summary>
    /// Creates a workshop and a vehicle at <paramref name="odometerKm"/> with an «Oil change» routine
    /// (10,000 km or 6 months) whose cycle started at 50,000 km on 2026-01-01.
    /// </summary>
    public static async Task<Guid> CreateVehicleWithOilChangeAsync(this HttpClient client, int odometerKm)
    {
        var workshop = await (await client.PostJsonAsync("/api/v1/workshops", new { name = "Taller de pruebas" })).ReadAsync<IdOnly>();
        var plate = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var vehicle = await (await client.PostJsonAsync("/api/v1/vehicles", new { plate, workshopId = workshop.Id, initialOdometerKm = odometerKm }))
            .ReadAsync<IdOnly>();
        (await client.PostJsonAsync($"/api/v1/vehicles/{vehicle.Id}/routines", new
        {
            name = "Oil change",
            intervalKm = 10_000,
            intervalMonths = 6,
            lastServiceKm = 50_000,
            lastServiceDate = "2026-01-01",
        })).EnsureSuccessStatusCode();
        return vehicle.Id;
    }
}

internal sealed record IdOnly(Guid Id);
