using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RutaViva.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(RutaVivaDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy("PostgreSQL reachable")
            : HealthCheckResult.Unhealthy("PostgreSQL unreachable");
}
