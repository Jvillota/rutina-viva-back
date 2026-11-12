namespace RutaViva.Infrastructure.Persistence;

// Persistence-only records: tables that are not domain aggregates (logs, outbox, inbox, demo state).

public sealed class OdometerReadingRecord
{
    public long Id { get; set; }

    public Guid VehicleId { get; set; }

    public int Km { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class OutboxMessage
{
    public const int MaxAttempts = 10;

    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}

public sealed class NotificationRecord
{
    public Guid Id { get; set; }

    public Guid OutboxMessageId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? VehicleId { get; set; }

    public Guid? ServiceOrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>Single row (Id = 1) with the demo clock offset shared by the API and the Worker.</summary>
public sealed class SystemClockRecord
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public long OffsetSeconds { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DailyCheckRunRecord
{
    public DateOnly BusinessDate { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public int RoutinesChanged { get; set; }

    public int OrdersCreated { get; set; }
}
