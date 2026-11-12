using Microsoft.EntityFrameworkCore;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Infrastructure.Persistence;

public sealed class RutaVivaDbContext(DbContextOptions<RutaVivaDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<MaintenanceRoutine> MaintenanceRoutines => Set<MaintenanceRoutine>();

    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();

    public DbSet<OdometerReadingRecord> OdometerReadings => Set<OdometerReadingRecord>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    public DbSet<SystemClockRecord> SystemClock => Set<SystemClockRecord>();

    public DbSet<DailyCheckRunRecord> DailyCheckRuns => Set<DailyCheckRunRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RutaVivaDbContext).Assembly);
    }
}
