using Conductor.Core.Application.Dashboard;
using Conductor.Core.Application.InstanceCollection;
using Conductor.Core.Application.Instances;
using Conductor.Core.Application.Queries;
using Conductor.Core.Application.Repositories;
using Conductor.Core.Application.Secrets;
using Conductor.Core.Application.Snapshots;
using Conductor.Infrastructure.Persistence.Sqlite.Dashboard;
using Conductor.Infrastructure.Persistence.Sqlite.Instances;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Conductor.Infrastructure.Persistence.Sqlite.Repositories;
using Conductor.Infrastructure.Persistence.Sqlite.Snapshots;
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
        services.AddScoped<IActiveRepositoryDashboardQuery, SqliteActiveRepositoryDashboardQuery>();

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
        services.AddScoped<IProjectListQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<IRepositoryImportService, SqliteRepositoryImportService>();
        services.AddScoped<IInstanceSummaryQueryService>(provider =>
            provider.GetRequiredService<SqliteProjectionQueryService>());
        services.AddScoped<ISecretDescriptorQueryService, SqliteSecretDescriptorQueryService>();
        services.AddScoped<IManualInstanceRegistrationService, SqliteManualInstanceRegistrationService>();
        services.AddScoped<IInstanceCollectionStore, SqliteInstanceCollectionStore>();
        services.AddScoped<IInstanceSnapshotStore, SqliteInstanceSnapshotStore>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
