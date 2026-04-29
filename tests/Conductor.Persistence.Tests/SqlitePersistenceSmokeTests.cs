using System.Data.Common;
using System.Globalization;
using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Fact]
    public async Task DevelopmentSeedData_Is_Idempotent_And_Populates_Dashboard_Data()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (ConductorDbContext dbContext = new(options))
        {
            await dbContext.Database.MigrateAsync();
            await DevelopmentSeedData.SeedAsync(dbContext);
            await DevelopmentSeedData.SeedAsync(dbContext);
        }

        Assert.Equal(2, await CountRowsAsync(connection, "Projects"));
        Assert.Equal(3, await CountRowsAsync(connection, "Repositories"));
        Assert.Equal(3, await CountRowsAsync(connection, "SymphonyInstances"));
        Assert.Equal(3, await CountRowsAsync(connection, "InstanceSnapshots"));
    }

    [Fact]
    public async Task ReadModelQueries_Return_Seeded_Dashboard_Projection()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (ConductorDbContext dbContext = new(options))
        {
            await dbContext.Database.MigrateAsync();
            await DevelopmentSeedData.SeedAsync(dbContext);
        }

        var queries = new EfConductorReadModelQueries(new TestDbContextFactory(options));

        ConductorDashboardSummary summary = await queries.GetDashboardSummaryAsync();

        Assert.Equal(2, summary.ProjectCount);
        Assert.Equal(3, summary.RepositoryCount);
        Assert.Equal(3, summary.InstanceCount);
        Assert.Equal(2, summary.NeedsAttentionCount);
        Assert.Contains(summary.ActiveRepositories, repository => repository.FullName == "ReleasedGroup/TheConductor");
        Assert.Contains(summary.NeedsAttention, repository => repository.HealthStatus == InstanceHealthStatus.Offline);
    }

    [Fact]
    public async Task DevelopmentBootstrap_Migrates_And_Seeds_Configured_Database()
    {
        string databasePath = CreateTemporaryDatabasePath();
        IConfiguration configuration = BuildConfiguration(databasePath);
        ServiceCollection services = new();

        services.AddConductorPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await provider.BootstrapDevelopmentDatabaseAsync(NullLogger.Instance);

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.OpenConnectionAsync();

        DbConnection connection = dbContext.Database.GetDbConnection();

        Assert.Equal(2, await CountRowsAsync(connection, "Projects"));
        Assert.Equal(3, await CountRowsAsync(connection, "Repositories"));
        Assert.Equal(3, await CountRowsAsync(connection, "SymphonyInstances"));
        Assert.Equal(3, await CountRowsAsync(connection, "InstanceSnapshots"));
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

    private static async Task<long> CountRowsAsync(DbConnection connection, string tableName)
    {
        object? value = await ExecuteScalarAsync(connection, $"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)};");

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    private sealed record RequiredIndex(string TableName, string[] Columns, bool IsUnique = false);

    private sealed record AppliedIndex(bool IsUnique, IReadOnlyList<string> Columns);

    private sealed class TestDbContextFactory(DbContextOptions<ConductorDbContext> options)
        : IDbContextFactory<ConductorDbContext>
    {
        public ConductorDbContext CreateDbContext() => new(options);
    }
}
