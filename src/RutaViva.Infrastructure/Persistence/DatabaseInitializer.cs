using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RutaViva.Infrastructure.Persistence.Seed;
using RutaViva.Infrastructure.Time;

namespace RutaViva.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public const string ConnectionStringName = "RutaViva";

    /// <summary>Only the API migrates on startup (Docker). The Worker never does, to avoid concurrent migrations.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations and seeds demo data, according to configuration. Called by the API host.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
        {
            logger.LogInformation("Applying database migrations");
            await provider.GetRequiredService<RutaVivaDbContext>().Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        if (provider.GetRequiredService<IOptions<DemoOptions>>().Value.SeedData)
        {
            await provider.GetRequiredService<DemoDataSeeder>().SeedAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
