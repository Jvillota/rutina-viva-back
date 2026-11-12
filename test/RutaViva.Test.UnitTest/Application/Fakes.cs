using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Test.UnitTest.Application;

internal sealed class InMemoryVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Items { get; } = [];

    public Task<Vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(v => v.Id == vehicleId));

    public Task<bool> PlateExistsAsync(string plate, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Exists(v => v.Plate == plate));

    public Task<IReadOnlyList<Vehicle>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Vehicle>>(Items);

    public Task<IReadOnlyList<Guid>> ListIdsForDailyCheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Items
            .Where(v => v.Routines.Any(r => r.Interval.Months is not null || r.Status == RoutineStatus.Overdue))
            .Select(v => v.Id)
            .ToList());

    public void Add(Vehicle vehicle) => Items.Add(vehicle);
}

internal sealed class InMemoryServiceOrderRepository : IServiceOrderRepository
{
    public List<ServiceOrder> Items { get; } = [];

    public Task<ServiceOrder?> GetByIdAsync(Guid serviceOrderId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(o => o.Id == serviceOrderId));

    public Task<IReadOnlyList<ServiceOrder>> ListAsync(ServiceOrderStatus? status, Guid? vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServiceOrder>>(Items
            .Where(o => (status is null || o.Status == status) && (vehicleId is null || o.VehicleId == vehicleId))
            .ToList());

    public Task<IReadOnlySet<Guid>> GetRoutineIdsWithOpenOrderAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Items.Where(o => o.VehicleId == vehicleId && o.IsOpen).Select(o => o.RoutineId).ToHashSet());

    public void Add(ServiceOrder serviceOrder) => Items.Add(serviceOrder);
}

internal sealed class InMemoryWorkshopRepository : IWorkshopRepository
{
    public List<Workshop> Items { get; } = [];

    public Task<Workshop?> GetByIdAsync(Guid workshopId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(w => w.Id == workshopId));

    public Task<IReadOnlyList<Workshop>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Workshop>>(Items);

    public void Add(Workshop workshop) => Items.Add(workshop);
}

/// <summary>Counts commits and can simulate lost concurrency races.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public int Resets { get; private set; }

    public int ConflictsToThrow { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ConflictsToThrow > 0)
        {
            ConflictsToThrow--;
            throw new ConcurrencyConflictException("simulated conflict");
        }

        Saves++;
        return Task.CompletedTask;
    }

    public void Reset() => Resets++;
}

internal sealed class InMemoryReadingLog : IOdometerReadingLog
{
    public List<OdometerReadingEntry> Items { get; } = [];

    public void Add(OdometerReadingEntry entry) => Items.Add(entry);

    public Task<IReadOnlyList<OdometerReadingEntry>> ListAsync(Guid vehicleId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OdometerReadingEntry>>(Items.Where(e => e.VehicleId == vehicleId).TakeLast(take).ToList());
}

internal sealed class InMemoryDailyCheckRuns : IDailyCheckRunRepository
{
    public List<DailyCheckRun> Items { get; } = [];

    public Task<DateOnly?> GetLastBusinessDateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count == 0 ? (DateOnly?)null : Items.Max(r => r.BusinessDate));

    public Task RecordAsync(DailyCheckRun run, CancellationToken cancellationToken)
    {
        Items.RemoveAll(r => r.BusinessDate == run.BusinessDate);
        Items.Add(run);
        return Task.CompletedTask;
    }
}

/// <summary>Wires the use cases against in-memory fakes and a controllable clock (UTC-5 business zone).</summary>
internal sealed class AppTestBed
{
    public AppTestBed(DateTimeOffset utcNow)
    {
        Time = new FakeTimeProvider(utcNow);
        Clock = new BusinessClock(Time, Options.Create(new BusinessOptions()));
        Retry = new ConcurrencyRetryExecutor(UnitOfWork, NullLogger<ConcurrencyRetryExecutor>.Instance);
        Opener = new OverdueRoutineOrderOpener(Orders);
    }

    public FakeTimeProvider Time { get; }

    public BusinessClock Clock { get; }

    public InMemoryVehicleRepository Vehicles { get; } = new();

    public InMemoryServiceOrderRepository Orders { get; } = new();

    public InMemoryWorkshopRepository Workshops { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public InMemoryReadingLog Readings { get; } = new();

    public InMemoryDailyCheckRuns Runs { get; } = new();

    public ConcurrencyRetryExecutor Retry { get; }

    public OverdueRoutineOrderOpener Opener { get; }

    public QuoteApprovalPolicy Policy { get; } = new();
}
