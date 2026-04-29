using Conductor.Core.Application.Queries;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
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

        void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
        {
            options
                .UseConductorSqlite(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<SqliteConnectionPragmaInterceptor>());
        }

        services.AddDbContextFactory<ConductorDbContext>(Configure);
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<ConductorDbContext>>().CreateDbContext());
        services.AddScoped<IConductorReadModelQueries, EfConductorReadModelQueries>();

        return services;
    }
}
