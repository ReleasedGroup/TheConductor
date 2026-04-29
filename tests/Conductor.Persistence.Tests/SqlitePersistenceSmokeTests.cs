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
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = CreateDbContext(connection);

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
    public async Task Migrated_Database_RoundTrips_Basic_Portfolio_Crud()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateMigratedDbContextAsync(connection);
        var ids = await SeedPortfolioAsync(connection);

        Assert.Equal("Active", await ExecuteScalarAsync<string>(connection, "SELECT Status FROM Projects WHERE Id = $id;", ("$id", ids.ProjectId)));
        Assert.Equal("Provisioned", await ExecuteScalarAsync<string>(connection, "SELECT Status FROM SymphonyInstances WHERE Id = $id;", ("$id", ids.InstanceId)));
        Assert.Equal("Healthy", await ExecuteScalarAsync<string>(connection, "SELECT HealthStatus FROM InstanceSnapshots WHERE Id = $id;", ("$id", ids.FirstSnapshotId)));

        await ExecuteNonQueryAsync(
            connection,
            """
            UPDATE Projects
            SET Status = $status,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """,
            ("$id", ids.ProjectId),
            ("$status", "Archived"),
            ("$updatedAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 2, 0, 0, TimeSpan.Zero))));

        await ExecuteNonQueryAsync(
            connection,
            """
            UPDATE SymphonyInstances
            SET Status = $status,
                HealthStatus = $healthStatus,
                LastHealthCheckAtUtc = $observedAtUtc,
                LastSeenAtUtc = $observedAtUtc
            WHERE Id = $id;
            """,
            ("$id", ids.InstanceId),
            ("$status", "Running"),
            ("$healthStatus", "Warning"),
            ("$observedAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 2, 5, 0, TimeSpan.Zero))));

        await ExecuteNonQueryAsync(connection, "DELETE FROM InstanceSnapshots WHERE Id = $id;", ("$id", ids.FirstSnapshotId));

        Assert.Equal("Archived", await ExecuteScalarAsync<string>(connection, "SELECT Status FROM Projects WHERE Id = $id;", ("$id", ids.ProjectId)));
        Assert.Equal("Running", await ExecuteScalarAsync<string>(connection, "SELECT Status FROM SymphonyInstances WHERE Id = $id;", ("$id", ids.InstanceId)));
        Assert.Equal("Warning", await ExecuteScalarAsync<string>(connection, "SELECT HealthStatus FROM SymphonyInstances WHERE Id = $id;", ("$id", ids.InstanceId)));
        Assert.Equal(0L, await ExecuteScalarAsync<long>(connection, "SELECT COUNT(*) FROM InstanceSnapshots WHERE Id = $id;", ("$id", ids.FirstSnapshotId)));
    }

    [Fact]
    public async Task Repository_Uniqueness_Rule_Prevents_Duplicate_Provider_Owner_Name()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateMigratedDbContextAsync(connection);
        var ids = await SeedPortfolioAsync(connection);

        SqliteException exception = await Assert.ThrowsAsync<SqliteException>(() => InsertRepositoryAsync(
            connection,
            Guid.NewGuid().ToString("D"),
            ids.ProjectId,
            "GitHub",
            "ReleasedGroup",
            "TheConductor"));

        Assert.Equal(19, exception.SqliteErrorCode);
    }

    [Fact]
    public async Task Projection_Query_Loads_Instance_Summaries_With_Latest_Snapshot()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateMigratedDbContextAsync(connection);
        var ids = await SeedPortfolioAsync(connection);
        string latestSnapshotId = Guid.NewGuid().ToString("D");

        await InsertSnapshotAsync(
            connection,
            latestSnapshotId,
            ids.InstanceId,
            new DateTimeOffset(2026, 4, 29, 1, 10, 0, TimeSpan.Zero),
            "Critical",
            """{"tracked":3}""");

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                repositories.Owner || '/' || repositories.Name AS RepositoryFullName,
                instances.DisplayName,
                instances.ExecutionMode,
                latestSnapshot.HealthStatus,
                latestSnapshot.StateJson,
                (
                    SELECT COUNT(*)
                    FROM InstanceSnapshots snapshotCount
                    WHERE snapshotCount.SymphonyInstanceId = instances.Id
                ) AS SnapshotCount
            FROM Repositories repositories
            INNER JOIN SymphonyInstances instances ON instances.RepositoryId = repositories.Id
            INNER JOIN InstanceSnapshots latestSnapshot ON latestSnapshot.Id = (
                SELECT candidate.Id
                FROM InstanceSnapshots candidate
                WHERE candidate.SymphonyInstanceId = instances.Id
                ORDER BY candidate.CapturedAtUtc DESC
                LIMIT 1
            )
            WHERE repositories.Id = $repositoryId;
            """;
        command.Parameters.AddWithValue("$repositoryId", ids.RepositoryId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("ReleasedGroup/TheConductor", reader.GetString(0));
        Assert.Equal("Primary", reader.GetString(1));
        Assert.Equal("LocalProcess", reader.GetString(2));
        Assert.Equal("Critical", reader.GetString(3));
        Assert.Equal("""{"tracked":3}""", reader.GetString(4));
        Assert.Equal(2, reader.GetInt32(5));
        Assert.False(await reader.ReadAsync());
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

    private static async Task<ConductorDbContext> CreateMigratedDbContextAsync(SqliteConnection connection)
    {
        ConductorDbContext dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        return dbContext;
    }

    private static ConductorDbContext CreateDbContext(SqliteConnection connection)
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ConductorDbContext(options);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<PortfolioIds> SeedPortfolioAsync(SqliteConnection connection)
    {
        string projectId = Guid.NewGuid().ToString("D");
        string repositoryId = Guid.NewGuid().ToString("D");
        string instanceId = Guid.NewGuid().ToString("D");
        string snapshotId = Guid.NewGuid().ToString("D");

        await InsertProjectAsync(connection, projectId);
        await InsertRepositoryAsync(connection, repositoryId, projectId, "GitHub", "ReleasedGroup", "TheConductor");
        await InsertInstanceAsync(connection, instanceId, repositoryId);
        await InsertSnapshotAsync(
            connection,
            snapshotId,
            instanceId,
            new DateTimeOffset(2026, 4, 29, 1, 0, 0, TimeSpan.Zero),
            "Healthy",
            """{"tracked":2}""");

        return new PortfolioIds(projectId, repositoryId, instanceId, snapshotId);
    }

    private static async Task InsertProjectAsync(SqliteConnection connection, string projectId)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO Projects (Id, Name, OwnerName, Status, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($id, $name, $ownerName, $status, $createdAtUtc, $updatedAtUtc);
            """,
            ("$id", projectId),
            ("$name", "Platform"),
            ("$ownerName", "Platform Engineering"),
            ("$status", "Active"),
            ("$createdAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero))),
            ("$updatedAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero))));
    }

    private static async Task InsertRepositoryAsync(
        SqliteConnection connection,
        string repositoryId,
        string projectId,
        string provider,
        string owner,
        string name)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO Repositories
                (Id, ProjectId, Provider, Owner, Name, DefaultBranch, CloneUrl, WebUrl, IsArchived, OpenIssueCount, PullRequestCount, ImportedAtUtc, UpdatedAtUtc)
            VALUES
                ($id, $projectId, $provider, $owner, $name, $defaultBranch, $cloneUrl, $webUrl, $isArchived, $openIssueCount, $pullRequestCount, $importedAtUtc, $updatedAtUtc);
            """,
            ("$id", repositoryId),
            ("$projectId", projectId),
            ("$provider", provider),
            ("$owner", owner),
            ("$name", name),
            ("$defaultBranch", "main"),
            ("$cloneUrl", $"https://github.com/{owner}/{name}.git"),
            ("$webUrl", $"https://github.com/{owner}/{name}"),
            ("$isArchived", false),
            ("$openIssueCount", 12),
            ("$pullRequestCount", 3),
            ("$importedAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 0, 5, 0, TimeSpan.Zero))),
            ("$updatedAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 0, 5, 0, TimeSpan.Zero))));
    }

    private static async Task InsertInstanceAsync(SqliteConnection connection, string instanceId, string repositoryId)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO SymphonyInstances
                (Id, RepositoryId, DisplayName, ExecutionMode, BaseUrl, Status, HealthStatus, DeliveryStatus, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($id, $repositoryId, $displayName, $executionMode, $baseUrl, $status, $healthStatus, $deliveryStatus, $createdAtUtc, $updatedAtUtc);
            """,
            ("$id", instanceId),
            ("$repositoryId", repositoryId),
            ("$displayName", "Primary"),
            ("$executionMode", "LocalProcess"),
            ("$baseUrl", "http://localhost:5001"),
            ("$status", "Provisioned"),
            ("$healthStatus", "Unknown"),
            ("$deliveryStatus", "Healthy"),
            ("$createdAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 0, 10, 0, TimeSpan.Zero))),
            ("$updatedAtUtc", FormatUtc(new DateTimeOffset(2026, 4, 29, 0, 10, 0, TimeSpan.Zero))));
    }

    private static async Task InsertSnapshotAsync(
        SqliteConnection connection,
        string snapshotId,
        string instanceId,
        DateTimeOffset capturedAtUtc,
        string healthStatus,
        string stateJson)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO InstanceSnapshots
                (Id, SymphonyInstanceId, CapturedAtUtc, HealthStatus, HealthJson, RuntimeJson, StateJson)
            VALUES
                ($id, $instanceId, $capturedAtUtc, $healthStatus, $healthJson, $runtimeJson, $stateJson);
            """,
            ("$id", snapshotId),
            ("$instanceId", instanceId),
            ("$capturedAtUtc", FormatUtc(capturedAtUtc)),
            ("$healthStatus", healthStatus),
            ("$healthJson", """{"status":"ok"}"""),
            ("$runtimeJson", """{"version":"1.0.0"}"""),
            ("$stateJson", stateJson));
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameters(command, parameters);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T?> ExecuteScalarAsync<T>(
        SqliteConnection connection,
        string commandText,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameters(command, parameters);

        object? value = await command.ExecuteScalarAsync();

        if (value is null || value is DBNull)
        {
            return default;
        }

        return (T)value;
    }

    private static void AddParameters(
        SqliteCommand command,
        params (string Name, object? Value)[] parameters)
    {
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
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

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

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

    private sealed record PortfolioIds(
        string ProjectId,
        string RepositoryId,
        string InstanceId,
        string FirstSnapshotId);

    private sealed record RequiredIndex(string TableName, string[] Columns, bool IsUnique = false);

    private sealed record AppliedIndex(bool IsUnique, IReadOnlyList<string> Columns);
}
