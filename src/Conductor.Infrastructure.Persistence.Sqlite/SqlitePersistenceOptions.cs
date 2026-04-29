namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class SqlitePersistenceOptions
{
    public const string ConnectionStringName = "Conductor";
    public const string DefaultConnectionString = "Data Source=./data/conductor.db;Cache=Shared";
    public const int DefaultBusyTimeoutMilliseconds = 5000;
}
