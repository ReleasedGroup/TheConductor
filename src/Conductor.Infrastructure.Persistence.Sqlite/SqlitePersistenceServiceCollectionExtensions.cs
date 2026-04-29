using Conductor.Core.Application.Dashboard;
using Conductor.Infrastructure.Persistence.Sqlite.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class SqlitePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddConductorPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(SqlitePersistenceOptions.ConnectionStringName)
            ?? SqlitePersistenceOptions.DefaultConnectionString;

        services.AddSingleton<SqliteConnectionPragmaInterceptor>();
        services.AddScoped<IActiveRepositoryDashboardQuery, SqliteActiveRepositoryDashboardQuery>();

        services.AddDbContext<ConductorDbContext>((serviceProvider, options) =>
        {
            options
                .UseConductorSqlite(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<SqliteConnectionPragmaInterceptor>());
        });

        return services;
    }
}
