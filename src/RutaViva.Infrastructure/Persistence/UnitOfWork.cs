using Microsoft.EntityFrameworkCore;
using Npgsql;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Common;
using RutaViva.Infrastructure.Persistence.Configurations;

namespace RutaViva.Infrastructure.Persistence;

/// <summary>
/// Commits the tracked changes. Optimistic concurrency failures (xmin) and violations of the «one open order»
/// index are translated to <see cref="ConcurrencyConflictException"/>, which the application retries.
/// </summary>
internal sealed class UnitOfWork(RutaVivaDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The row was modified by another transaction.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ServiceOrderConfiguration.OpenOrderIndexName,
        })
        {
            throw new ConcurrencyConflictException("Another transaction opened the service order first.", ex);
        }
    }

    public void Reset() => db.ChangeTracker.Clear();
}
