using System.Data.Common;
using System.Globalization;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Persistence.Tests;

public sealed class SqlitePersistenceSmokeTests
{
    private static readonly string[] ExpectedTables =
    [
        "Projects",
        "Repositories",
        "SymphonyInstances",
        "WorkflowProfiles",
        "SymphonyReleaseArtifacts",
        "InstanceSnapshots",
        "TrackedIssues",
        "Runs",
        "RunAttempts",
        "Events",
        "Alerts",
        "Reports",
        "SecretDescriptors",
        "AuditEvents",
        "BackgroundOperations",
    ];

    private static readonly RequiredIndex[] RequiredIndexes =
    [
        new("Repositories", ["ProjectId"]),
        new("Repositories", ["Provider", "Owner", "Name"], IsUnique: true),
        new("SymphonyInstances", ["RepositoryId"]),
        new("SymphonyInstances", ["Status", "HealthStatus"]),
        new("SymphonyInstances", ["ExecutionMode"]),
        new("InstanceSnapshots", ["SymphonyInstanceId", "CapturedAtUtc"]),
        new("TrackedIssues", ["RepositoryId", "GitHubIssueNumber"]),
        new("TrackedIssues", ["RepositoryId", "SymphonyStatus", "IsBlocked"]),
        new("Runs", ["SymphonyInstanceId", "Status", "StartedAtUtc"]),
        new("Runs", ["RepositoryId", "GitHubIssueNumber"]),
        new("Events", ["SymphonyInstanceId", "OccurredAtUtc"]),
        new("Events", ["RepositoryId", "OccurredAtUtc"]),
        new("Alerts", ["Status", "Severity", "CreatedAtUtc"]),
        new("Reports", ["ReportType", "GeneratedAtUtc"]),
        new("AuditEvents", ["ActorUserId", "OccurredAtUtc"]),
        new("SecretDescriptors", ["ScopeType", "ScopeId", "SecretType"]),
        new("BackgroundOperations", ["Status", "CreatedAtUtc"]),
    ];

    [Fact]
    public async Task Initial_Migration_Creates_Required_Tables_And_Indexes()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ConductorDbContext dbContext = new(options);
        await dbContext.Database.MigrateAsync();

        HashSet<string> tableNames = await LoadTableNamesAsync(connection);
        IReadOnlyDictionary<string, IReadOnlyList<AppliedIndex>> indexesByTable = await LoadIndexesAsync(
            connection,
            RequiredIndexes.Select(index => index.TableName).Distinct(StringComparer.Ordinal));

        foreach (string tableName in ExpectedTables)
        {
            Assert.Contains(tableName, tableNames);
        }

        foreach (RequiredIndex requiredIndex in RequiredIndexes)
        {
            Assert.True(
                indexesByTable.TryGetValue(requiredIndex.TableName, out IReadOnlyList<AppliedIndex>? indexes),
                $"Expected at least one index on {requiredIndex.TableName}.");

            Assert.Contains(
                indexes,
                index => index.Columns.SequenceEqual(requiredIndex.Columns, StringComparer.Ordinal)
                    && (!requiredIndex.IsUnique || index.IsUnique));
        }

        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task AddConductorPersistence_Registers_Configured_DbContext()
    {
        string databasePath = CreateTemporaryDatabasePath();
        IConfiguration configuration = BuildConfiguration(databasePath);
        ServiceCollection services = new();

        services.AddConductorPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)));
    }

    [Fact]
    public async Task AddConductorPersistence_Applies_Required_Sqlite_Pragmas()
    {
        string databasePath = CreateTemporaryDatabasePath();
        IConfiguration configuration = BuildConfiguration(databasePath);
        ServiceCollection services = new();

        services.AddConductorPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        DbConnection connection = dbContext.Database.GetDbConnection();

        Assert.Equal(1, await ExecuteLongPragmaAsync(connection, "PRAGMA foreign_keys;"));
        Assert.Equal(SqlitePersistenceOptions.DefaultBusyTimeoutMilliseconds, await ExecuteLongPragmaAsync(connection, "PRAGMA busy_timeout;"));
        Assert.Equal("wal", await ExecuteTextPragmaAsync(connection, "PRAGMA journal_mode;"));
    }

    private static async Task<HashSet<string>> LoadTableNamesAsync(SqliteConnection connection)
    {
        HashSet<string> tableNames = new(StringComparer.Ordinal);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
                AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<AppliedIndex>>> LoadIndexesAsync(
        SqliteConnection connection,
        IEnumerable<string> tableNames)
    {
        Dictionary<string, IReadOnlyList<AppliedIndex>> indexesByTable = new(StringComparer.Ordinal);

        foreach (string tableName in tableNames)
        {
            List<AppliedIndex> indexes = [];

            await using SqliteCommand indexListCommand = connection.CreateCommand();
            indexListCommand.CommandText = $"PRAGMA index_list({QuoteIdentifier(tableName)});";

            await using SqliteDataReader indexListReader = await indexListCommand.ExecuteReaderAsync();
            while (await indexListReader.ReadAsync())
            {
                string indexName = indexListReader.GetString(1);
                bool isUnique = indexListReader.GetInt32(2) == 1;
                IReadOnlyList<string> columns = await LoadIndexColumnsAsync(connection, indexName);

                indexes.Add(new AppliedIndex(isUnique, columns));
            }

            indexesByTable[tableName] = indexes;
        }

        return indexesByTable;
    }

    private static async Task<IReadOnlyList<string>> LoadIndexColumnsAsync(SqliteConnection connection, string indexName)
    {
        List<string> columns = [];

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info({QuoteIdentifier(indexName)});";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(2));
        }

        return columns;
    }

    private static IConfiguration BuildConfiguration(string databasePath)
    {
        Dictionary<string, string?> values = new()
        {
            [$"ConnectionStrings:{SqlitePersistenceOptions.ConnectionStringName}"] = $"Data Source={databasePath};Cache=Shared",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string CreateTemporaryDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            "conductor-persistence-tests",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "conductor.db");

    private static async Task<long> ExecuteLongPragmaAsync(DbConnection connection, string commandText)
    {
        object? value = await ExecuteScalarAsync(connection, commandText);

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ExecuteTextPragmaAsync(DbConnection connection, string commandText)
    {
        object? value = await ExecuteScalarAsync(connection, commandText);

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static async Task<object?> ExecuteScalarAsync(DbConnection connection, string commandText)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        return await command.ExecuteScalarAsync();
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    private sealed record RequiredIndex(string TableName, string[] Columns, bool IsUnique = false);

    private sealed record AppliedIndex(bool IsUnique, IReadOnlyList<string> Columns);
}
