using Microsoft.Data.Sqlite;

namespace Conductor.Infrastructure.Persistence.Sqlite;

internal static class SqliteDatabaseDirectory
{
    public static void EnsureExists(string connectionString)
    {
        SqliteConnectionStringBuilder builder = new(connectionString);

        if (IsInMemoryDatabase(builder))
        {
            return;
        }

        string? directory = GetDirectory(builder.DataSource);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static bool IsInMemoryDatabase(SqliteConnectionStringBuilder builder) =>
        builder.Mode == SqliteOpenMode.Memory
        || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
        || builder.DataSource.Contains("mode=memory", StringComparison.OrdinalIgnoreCase);

    private static string? GetDirectory(string dataSource)
    {
        string path = dataSource;

        if (Uri.TryCreate(dataSource, UriKind.Absolute, out Uri? uri) && uri.IsFile)
        {
            path = uri.LocalPath;
        }

        string? directory = Path.GetDirectoryName(path);

        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }
}
