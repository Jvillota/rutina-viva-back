using Microsoft.EntityFrameworkCore;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Infrastructure.Persistence;

internal sealed class VehicleRepository(RutaVivaDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        db.Vehicles.Include(v => v.Routines).FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

    public Task<bool> PlateExistsAsync(string plate, CancellationToken cancellationToken) =>
        db.Vehicles.AnyAsync(v => v.Plate == plate, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListAsync(CancellationToken cancellationToken) =>
        await db.Vehicles.AsNoTracking().Include(v => v.Routines).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListIdsForDailyCheckAsync(CancellationToken cancellationToken) =>
        await db.Vehicles
            .Where(v => v.Routines.Any(r => r.Interval.Months != null || r.Status == RoutineStatus.Overdue))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Vehicle vehicle) => db.Vehicles.Add(vehicle);
}

internal sealed class ServiceOrderRepository(RutaVivaDbContext db) : IServiceOrderRepository
{
    public Task<ServiceOrder?> GetByIdAsync(Guid serviceOrderId, CancellationToken cancellationToken) =>
        db.ServiceOrders.Include(o => o.Transitions).FirstOrDefaultAsync(o => o.Id == serviceOrderId, cancellationToken);

    public async Task<IReadOnlyList<ServiceOrder>> ListAsync(ServiceOrderStatus? status, Guid? vehicleId, CancellationToken cancellationToken)
    {
        var query = db.ServiceOrders.AsNoTracking().Include(o => o.Transitions).AsQueryable();
        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (vehicleId is not null)
        {
            query = query.Where(o => o.VehicleId == vehicleId);
        }

        return await query.AsSplitQuery().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<Guid>> GetRoutineIdsWithOpenOrderAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var ids = await db.ServiceOrders
            .Where(o => o.VehicleId == vehicleId
                && o.Status != ServiceOrderStatus.Completed
                && o.Status != ServiceOrderStatus.Cancelled)
            .Select(o => o.RoutineId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Orders opened in this same unit of work are not in the database yet.
        var pending = db.ChangeTracker.Entries<ServiceOrder>()
            .Where(e => e.State == EntityState.Added && e.Entity.VehicleId == vehicleId && e.Entity.IsOpen)
            .Select(e => e.Entity.RoutineId);

        return ids.Concat(pending).ToHashSet();
    }

    public void Add(ServiceOrder serviceOrder) => db.ServiceOrders.Add(serviceOrder);
}

internal sealed class WorkshopRepository(RutaVivaDbContext db) : IWorkshopRepository
{
    public Task<Workshop?> GetByIdAsync(Guid workshopId, CancellationToken cancellationToken) =>
        db.Workshops.FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

    public async Task<IReadOnlyList<Workshop>> ListAsync(CancellationToken cancellationToken) =>
        await db.Workshops.AsNoTracking().OrderBy(w => w.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Workshop workshop) => db.Workshops.Add(workshop);
}

internal sealed class OdometerReadingLog(RutaVivaDbContext db) : IOdometerReadingLog
{
    public void Add(OdometerReadingEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        db.OdometerReadings.Add(new OdometerReadingRecord
        {
            VehicleId = entry.VehicleId,
            Km = entry.Km,
            Outcome = entry.Outcome.ToString(),
            ReceivedAt = entry.ReceivedAt,
        });
    }

    public async Task<IReadOnlyList<OdometerReadingEntry>> ListAsync(Guid vehicleId, int take, CancellationToken cancellationToken)
    {
        var rows = await db.OdometerReadings.AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.ReceivedAt)
            .ThenByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new OdometerReadingEntry(r.VehicleId, r.Km, Enum.Parse<OdometerReadingOutcome>(r.Outcome), r.ReceivedAt)).ToList();
    }
}

internal sealed class NotificationInbox(RutaVivaDbContext db) : INotificationInbox
{
    public async Task<IReadOnlyList<NotificationEntry>> ListAsync(int take, CancellationToken cancellationToken) =>
        await db.Notifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationEntry(n.Id, n.Type, n.Title, n.Message, n.VehicleId, n.ServiceOrderId, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> MarkAsReadAsync(Guid notificationId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        var updated = await db.Notifications
            .Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, n => n.ReadAt ?? readAt), cancellationToken)
            .ConfigureAwait(false);
        return updated > 0;
    }
}

internal sealed class DailyCheckRunRepository(RutaVivaDbContext db) : IDailyCheckRunRepository
{
    public async Task<DateOnly?> GetLastBusinessDateAsync(CancellationToken cancellationToken) =>
        await db.DailyCheckRuns.AsNoTracking()
            .OrderByDescending(r => r.BusinessDate)
            .Select(r => (DateOnly?)r.BusinessDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task RecordAsync(DailyCheckRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO daily_check_runs (business_date, started_at, finished_at, routines_changed, orders_created)
            VALUES ({run.BusinessDate}, {run.StartedAt}, {run.FinishedAt}, {run.RoutinesChanged}, {run.OrdersCreated})
            ON CONFLICT (business_date) DO UPDATE SET
                finished_at = EXCLUDED.finished_at,
                routines_changed = daily_check_runs.routines_changed + EXCLUDED.routines_changed,
                orders_created = daily_check_runs.orders_created + EXCLUDED.orders_created
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
