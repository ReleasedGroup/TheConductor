using Conductor.Core.Application;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class SqlitePersistenceInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Persistence.Sqlite",
        "EF Core SQLite persistence, migrations, and repository adapters.");
}
