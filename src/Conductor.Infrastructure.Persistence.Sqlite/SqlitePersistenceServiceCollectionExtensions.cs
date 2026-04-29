using Conductor.Core.Application.Queries;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class SqlitePersistenceServiceCollectionExtensions
{
    private const string DefaultConnectionString = "Data Source=./data/conductor.db;Cache=Shared";

    public static IServiceCollection AddConductorPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Conductor") ?? DefaultConnectionString;

        services.AddDbContext<ConductorDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<SqliteProjectionQueryService>();
        services.AddScoped<IDashboardQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<IRepositoryListQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<IInstanceSummaryQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());

        return services;
    }
}
