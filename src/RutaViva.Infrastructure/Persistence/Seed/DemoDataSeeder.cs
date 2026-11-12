using Microsoft.EntityFrameworkCore;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Domain.Workshops;

namespace RutaViva.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo data covering every interesting situation: up to date, close to the kilometer threshold, close to the
/// time threshold, km-only / time-only / both intervals, and an already overdue routine with its order.
/// Goes through the domain, so events and orders are produced exactly as in real use.
/// </summary>
internal sealed class DemoDataSeeder(RutaVivaDbContext db, IBusinessClock clock)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Workshops.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var (today, now) = (clock.Today, clock.Now);
        var north = Workshop.Register("Taller Autoservicio Norte").Value;
        var south = Workshop.Register("Taller Express Sur").Value;
        db.Workshops.AddRange(north, south);

        // Up to date on every routine.
        var v1 = Vehicle.Register("RVA101", north.Id, 45_000, now).Value;
        Add(v1, "Cambio de aceite", 10_000, 6, 40_000, today.AddMonths(-2));
        Add(v1, "Llantas", 40_000, null, 20_000, null);

        // Oil change due at 59,500: a reading of 58,500 makes it due soon, 59,500 makes it overdue.
        var v2 = Vehicle.Register("RVB202", north.Id, 58_300, now).Value;
        Add(v2, "Cambio de aceite", 10_000, 6, 49_500, today.AddMonths(-3));
        Add(v2, "Frenos", 20_000, 12, 50_000, today.AddMonths(-4));

        // Time only, about one month left: advancing the clock ~15 days makes it due soon, ~31 days overdue.
        var v3 = Vehicle.Register("RVC303", south.Id, 30_500, now).Value;
        Add(v3, "Revisión técnico-mecánica", null, 6, null, today.AddMonths(-5));

        // Already overdue: the order is waiting for the workshop's quote.
        var v4 = Vehicle.Register("RVD404", south.Id, 61_200, now).Value;
        Add(v4, "Cambio de aceite", 10_000, 6, 51_000, today.AddMonths(-4));
        Add(v4, "Alineación y balanceo", 15_000, null, 55_000, null);

        db.Vehicles.AddRange(v1, v2, v3, v4);
        foreach (var vehicle in new[] { v1, v2, v3, v4 })
        {
            foreach (var routine in vehicle.OverdueRoutines())
            {
                db.ServiceOrders.Add(ServiceOrder.Open(vehicle, routine, today, now));
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        void Add(Vehicle vehicle, string name, int? km, int? months, int? lastKm, DateOnly? lastDate) =>
            vehicle.AddRoutine(name, MaintenanceInterval.Create(km, months).Value, lastKm, lastDate, today, now);
    }
}
