using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RutaViva.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model when adding migrations or generating the SQL script.
/// It never opens a connection, so it carries no credentials.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RutaVivaDbContext>
{
    public RutaVivaDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RutaVivaDbContext>()
            .UseNpgsql("Host=localhost;Database=rutaviva")
            .UseSnakeCaseNamingConvention()
            .Options);
}
