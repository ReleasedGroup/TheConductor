using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContextFactory : IDesignTimeDbContextFactory<ConductorDbContext>
{
    public ConductorDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ConductorDbContext> options = new();
        options.UseSqlite(SqlitePersistenceServiceCollectionExtensions.DefaultConnectionString);

        return new ConductorDbContext(options.Options);
    }
}
