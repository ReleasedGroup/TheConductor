using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class SqlitePersistenceApplicationBuilderExtensions
{
    public static async Task ApplyConductorPersistenceMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
