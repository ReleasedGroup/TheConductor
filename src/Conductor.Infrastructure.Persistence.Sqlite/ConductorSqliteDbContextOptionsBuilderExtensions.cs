using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite;

internal static class ConductorSqliteDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseConductorSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        SqliteDatabaseDirectory.EnsureExists(connectionString);

        return optionsBuilder.UseSqlite(
            connectionString,
            sqliteOptions => sqliteOptions.MigrationsAssembly(typeof(ConductorDbContext).Assembly.GetName().Name!));
    }
}
