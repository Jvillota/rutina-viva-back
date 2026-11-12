using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RutaViva.Domain.Vehicles;

namespace RutaViva.Infrastructure.Persistence.Configurations;

internal sealed class OdometerReadingConfiguration : IEntityTypeConfiguration<OdometerReadingRecord>
{
    public void Configure(EntityTypeBuilder<OdometerReadingRecord> builder)
    {
        builder.ToTable("odometer_readings");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityAlwaysColumn();
        builder.Property(r => r.Outcome).HasMaxLength(20).IsRequired();
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.VehicleId, r.ReceivedAt }).IsDescending(false, true).HasDatabaseName("ix_odometer_readings_vehicle");
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL").HasDatabaseName("ix_outbox_pending");
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasMaxLength(100).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.HasIndex(n => n.OutboxMessageId).IsUnique().HasDatabaseName("ux_notifications_outbox");
        builder.HasIndex(n => n.CreatedAt).IsDescending().HasDatabaseName("ix_notifications_created_at");
    }
}

internal sealed class SystemClockConfiguration : IEntityTypeConfiguration<SystemClockRecord>
{
    public void Configure(EntityTypeBuilder<SystemClockRecord> builder)
    {
        builder.ToTable("system_clock", t =>
        {
            t.HasCheckConstraint("ck_system_clock_singleton", "id = 1");
            t.HasCheckConstraint("ck_system_clock_offset_forward", "offset_seconds >= 0");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
    }
}

internal sealed class DailyCheckRunConfiguration : IEntityTypeConfiguration<DailyCheckRunRecord>
{
    public void Configure(EntityTypeBuilder<DailyCheckRunRecord> builder)
    {
        builder.ToTable("daily_check_runs");
        builder.HasKey(r => r.BusinessDate);
    }
}
