using RutaViva.Domain.Vehicles;

namespace RutaViva.Application.Abstractions.Persistence;

public sealed record OdometerReadingEntry(Guid VehicleId, int Km, OdometerReadingOutcome Outcome, DateTimeOffset ReceivedAt);

/// <summary>Audit log of every reading received, applied or ignored.</summary>
public interface IOdometerReadingLog
{
    void Add(OdometerReadingEntry entry);

    Task<IReadOnlyList<OdometerReadingEntry>> ListAsync(Guid vehicleId, int take, CancellationToken cancellationToken);
}

public sealed record NotificationEntry(
    Guid Id,
    string Type,
    string Title,
    string Message,
    Guid? VehicleId,
    Guid? ServiceOrderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

/// <summary>The fleet manager inbox, filled by the event publisher.</summary>
public interface INotificationInbox
{
    Task<IReadOnlyList<NotificationEntry>> ListAsync(int take, CancellationToken cancellationToken);

    /// <returns>False when the notification does not exist.</returns>
    Task<bool> MarkAsReadAsync(Guid notificationId, DateTimeOffset readAt, CancellationToken cancellationToken);
}

public sealed record DailyCheckRun(DateOnly BusinessDate, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, int RoutinesChanged, int OrdersCreated);

public interface IDailyCheckRunRepository
{
    Task<DateOnly?> GetLastBusinessDateAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the run of that business date.</summary>
    Task RecordAsync(DailyCheckRun run, CancellationToken cancellationToken);
}
