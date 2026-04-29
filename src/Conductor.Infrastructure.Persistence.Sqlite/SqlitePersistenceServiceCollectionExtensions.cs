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

        return services;
    }
}
