using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Conductor.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteConnectionPragmaInterceptor : DbConnectionInterceptor
{
    private static readonly string[] RequiredPragmas =
    [
        "PRAGMA foreign_keys=ON;",
        $"PRAGMA busy_timeout={SqlitePersistenceOptions.DefaultBusyTimeoutMilliseconds};",
        "PRAGMA journal_mode=WAL;",
    ];

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ConfigureConnection(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ConfigureConnectionAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void ConfigureConnection(DbConnection connection)
    {
        if (connection is not SqliteConnection)
        {
            return;
        }

        foreach (string pragma in RequiredPragmas)
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }
    }

    private static async Task ConfigureConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not SqliteConnection)
        {
            return;
        }

        foreach (string pragma in RequiredPragmas)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = pragma;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
