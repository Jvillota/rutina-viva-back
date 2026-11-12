using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Infrastructure.Persistence.Configurations;

internal static class ConcurrencyToken
{
    /// <summary>PostgreSQL system column xmin changes on every row update: a free optimistic concurrency token.</summary>
    public static void AddXmin<T>(this EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
}

internal sealed class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.ToTable("workshops");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Name).HasMaxLength(Workshop.MaxNameLength).IsRequired();
        builder.Ignore(w => w.DomainEvents);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", t => t.HasCheckConstraint("ck_vehicles_odometer_not_negative", "odometer_km >= 0"));
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Plate).HasMaxLength(Vehicle.MaxPlateLength).IsRequired();
        builder.HasIndex(v => v.Plate).IsUnique().HasDatabaseName("ux_vehicles_plate");
        builder.Property(v => v.OdometerKm);
        builder.Property(v => v.CreatedAt);
        builder.HasOne<Workshop>().WithMany().HasForeignKey(v => v.WorkshopId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Routines).WithOne().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Routines).HasField("_routines").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(v => v.DomainEvents);
        builder.AddXmin();
    }
}

internal sealed class MaintenanceRoutineConfiguration : IEntityTypeConfiguration<MaintenanceRoutine>
{
    public void Configure(EntityTypeBuilder<MaintenanceRoutine> builder)
    {
        builder.ToTable("maintenance_routines", t =>
            t.HasCheckConstraint("ck_routines_interval", "interval_km IS NOT NULL OR interval_months IS NOT NULL"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(MaintenanceRoutine.MaxNameLength).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.VehicleId, r.Name }).IsUnique().HasDatabaseName("ux_routines_vehicle_name");

        builder.OwnsOne(r => r.Interval, interval =>
        {
            interval.Property(i => i.Kilometers).HasColumnName("interval_km");
            interval.Property(i => i.Months).HasColumnName("interval_months");
        });
        builder.Navigation(r => r.Interval).IsRequired();

        builder.AddXmin();
    }
}
