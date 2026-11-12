using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RutaViva.Application.Abstractions.Messaging;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Infrastructure.Messaging;
using RutaViva.Infrastructure.Persistence;
using RutaViva.Infrastructure.Persistence.Seed;
using RutaViva.Infrastructure.Time;

namespace RutaViva.Infrastructure;

public static class RegisterInfrastructureDI
{
    /// <summary>
    /// The only place that knows EF Core and PostgreSQL. The connection string comes from
    /// <c>ConnectionStrings:RutaViva</c> (environment variable or user-secrets), never from versioned files.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<DemoOptions>().Bind(configuration.GetSection(DemoOptions.SectionName));

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing connection string 'ConnectionStrings:{DatabaseOptions.ConnectionStringName}'. " +
                "Set it with the environment variable ConnectionStrings__RutaViva or with dotnet user-secrets (see README).");
        }

        services.AddSingleton<DomainEventsToOutboxInterceptor>();
        services.AddDbContext<RutaVivaDbContext>((sp, options) => options
            // No EnableRetryOnFailure: it forbids the explicit transaction of the outbox processor, and concurrency
            // conflicts are already retried by the application (ConcurrencyRetryExecutor).
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DomainEventsToOutboxInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
        services.AddScoped<IWorkshopRepository, WorkshopRepository>();
        services.AddScoped<IOdometerReadingLog, OdometerReadingLog>();
        services.AddScoped<INotificationInbox, NotificationInbox>();
        services.AddScoped<IDailyCheckRunRepository, DailyCheckRunRepository>();

        services.AddScoped<IEventPublisher, NotificationEventPublisher>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<DemoDataSeeder>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        AddTime(services, configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions());
        return services;
    }

    private static void AddTime(IServiceCollection services, DemoOptions demo)
    {
        if (demo.Enabled)
        {
            services.AddSingleton<DemoTimeProvider>();
            services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<DemoTimeProvider>());
            services.AddScoped<IDemoClock>(sp => new DemoClock(
                sp.GetRequiredService<RutaVivaDbContext>(),
                sp.GetRequiredService<DemoTimeProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DemoOptions>>()));
            services.AddHostedService<DemoClockSynchronizer>();
        }
        else
        {
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IDemoClock>(sp => new DemoClock(
                sp.GetRequiredService<RutaVivaDbContext>(),
                null,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DemoOptions>>()));
        }
    }
}
