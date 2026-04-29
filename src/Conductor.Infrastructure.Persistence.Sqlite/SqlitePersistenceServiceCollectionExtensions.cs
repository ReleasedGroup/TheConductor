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

        void Configure(DbContextOptionsBuilder options) => options.UseSqlite(connectionString);

        services.AddDbContextFactory<ConductorDbContext>(Configure);
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<ConductorDbContext>>().CreateDbContext());
        services.AddScoped<IConductorReadModelQueries, EfConductorReadModelQueries>();

        return services;
    }
}
