using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Infrastructure.Persistence.Configurations;

internal sealed class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    /// <summary>Rule 3 enforced by the database: at most one open order per vehicle and routine.</summary>
    public const string OpenOrderIndexName = "ux_service_orders_open_per_routine";

    public void Configure(EntityTypeBuilder<ServiceOrder> builder)
    {
        builder.ToTable("service_orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.VehiclePlate).HasMaxLength(Vehicle.MaxPlateLength).IsRequired();
        builder.Property(o => o.RoutineName).HasMaxLength(MaintenanceRoutine.MaxNameLength).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.ApprovalType).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.CancellationReason).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.QuoteAmountCop).HasPrecision(18, 2);

        builder.HasOne<Vehicle>().WithMany().HasForeignKey(o => o.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MaintenanceRoutine>().WithMany().HasForeignKey(o => o.RoutineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Workshop>().WithMany().HasForeignKey(o => o.WorkshopId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.VehicleId, o.RoutineId })
            .IsUnique()
            .HasFilter("status NOT IN ('Completed', 'Cancelled')")
            .HasDatabaseName(OpenOrderIndexName);
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_service_orders_status");

        builder.HasMany(o => o.Transitions).WithOne().HasForeignKey("ServiceOrderId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Transitions).HasField("_transitions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.IsOpen);
        builder.Ignore(o => o.AwaitingClientApproval);
        builder.Ignore(o => o.DomainEvents);
        builder.AddXmin();
    }
}

internal sealed class ServiceOrderTransitionConfiguration : IEntityTypeConfiguration<ServiceOrderTransition>
{
    public void Configure(EntityTypeBuilder<ServiceOrderTransition> builder)
    {
        builder.ToTable("service_order_transitions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityAlwaysColumn();
        builder.Property(t => t.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Actor).HasConversion<string>().HasMaxLength(20);
    }
}
