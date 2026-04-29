using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContextFactory : IDesignTimeDbContextFactory<ConductorDbContext>
{
    public ConductorDbContext CreateDbContext(string[] args)
    {
        string connectionString = ResolveConnectionString(args);
        SqliteConnectionPragmaInterceptor pragmaInterceptor = new();

        DbContextOptionsBuilder<ConductorDbContext> optionsBuilder = new();
        optionsBuilder
            .UseConductorSqlite(connectionString)
            .AddInterceptors(pragmaInterceptor);

        return new ConductorDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString(string[] args) =>
        args.FirstOrDefault(argument => !string.IsNullOrWhiteSpace(argument))
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Conductor")
        ?? SqlitePersistenceOptions.DefaultConnectionString;
}
