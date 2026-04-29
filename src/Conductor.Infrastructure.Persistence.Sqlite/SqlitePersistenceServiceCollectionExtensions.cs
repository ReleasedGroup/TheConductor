using Conductor.Core.Application.Instances;
using Conductor.Core.Application.Queries;
using Conductor.Infrastructure.Persistence.Sqlite.Instances;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.AddDbContext<ConductorDbContext>((serviceProvider, options) =>
        {
            options
                .UseConductorSqlite(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<SqliteConnectionPragmaInterceptor>());
        });

        services.AddScoped<SqliteProjectionQueryService>();
        services.AddScoped<IDashboardQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<IRepositoryListQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<IInstanceSummaryQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<IManualInstanceRegistrationService, SqliteManualInstanceRegistrationService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
