using System.Data.Common;
using System.Globalization;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Operations;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Reports;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.SymphonyReleases;
using Conductor.Core.Domain.Workflows;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DomainEvent = Conductor.Core.Domain.Events.Event;

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
    }

    [Fact]
    public async Task DbContext_Persists_Domain_Entities_With_Configured_Converters()
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using ConductorDbContext dbContext = new(options);

        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();
        WorkflowProfileId workflowProfileId = WorkflowProfileId.New();
        SecretId secretId = SecretId.New();
        RunId runId = RunId.New();

        var project = new Project(
            projectId,
            "Platform",
            "ReleasedGroup",
            "Internal orchestration",
            "main",
            ProjectStatus.Active,
            now,
            now);
        var repository = new Repository(
            repositoryId,
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "TheConductor",
            "main",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            RepositoryVisibility.Public,
            isArchived: false,
            projectId,
            lastSyncedAtUtc: now,
            RepositoryOrchestrationStatus.Eligible,
            orchestrationStatusReason: null);
        var secret = new SecretDescriptor(
            secretId,
            "Repository GitHub token",
            SecretType.GitHubToken,
            SecretScopeType.Repository,
            repositoryId.ToString(),
            now);
        var instance = new SymphonyInstance(
            instanceId,
            repositoryId,
            "TheConductor main",
            ExecutionMode.Docker,
            new Uri("http://localhost:8080"),
            now,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy,
            port: 8080,
            gitHubCredentialSecretId: secretId,
            gitHubCredentialInheritanceMode: CredentialInheritanceMode.SpecificSecret,
            workflowPath: "./instances/workflow.md",
            dataPath: "./instances/data",
            lastStartedAtUtc: now,
            lastSeenAtUtc: now);
        var workflowProfile = new WorkflowProfile(
            workflowProfileId,
            "Default",
            "# WORKFLOW",
            now);
        var releaseArtifact = new SymphonyReleaseArtifact(
            "v1.2.3",
            "symphony-linux-x64.zip",
            new Uri("https://example.com/releases/v1.2.3/symphony-linux-x64.zip"),
            now,
            "sha256:abc");
        var snapshot = new InstanceSnapshot(
            InstanceSnapshotId.New(),
            instanceId,
            now.AddMinutes(5),
            InstanceHealthStatus.Healthy,
            """{"status":"ok"}""",
            """{"version":"1.2.3"}""",
            """{"running":1}""",
            activeIssueCount: 3,
            runningSessionCount: 1,
            retryQueueCount: 0,
            failedRunCount: 0,
            tokenInputTotal: 100,
            tokenOutputTotal: 40);
        var issue = new TrackedIssue(
            TrackedIssueId.New(),
            repositoryId,
            gitHubIssueNumber: 15,
            "Configure EF Core DbContext mappings",
            TrackedIssueState.Open,
            """["persistence"]""",
            "Sprint 1",
            """["nickbeau"]""",
            new Uri("https://github.com/ReleasedGroup/TheConductor/issues/15"),
            SymphonyIssueStatus.Running,
            RunStatus.Running,
            now,
            isBlocked: false,
            blockerReason: null);
        var run = new Run(
            runId,
            instanceId,
            repositoryId,
            gitHubIssueNumber: 15,
            symphonyRunId: "symphony-run-15",
            RunStatus.Running,
            now,
            finishedAtUtc: null,
            attemptCount: 1,
            tokenInput: 100,
            tokenOutput: 40,
            errorSummary: null,
            branchName: "symphony/15",
            pullRequestUrl: new Uri("https://github.com/ReleasedGroup/TheConductor/pull/126"));
        var attempt = new RunAttempt(
            RunAttemptId.New(),
            runId,
            attemptNumber: 1,
            RunStatus.Running,
            now,
            finishedAtUtc: null,
            exitReason: null,
            errorDetail: null);
        var recordedEvent = new DomainEvent(
            EventId.New(),
            instanceId,
            repositoryId,
            issueNumber: 15,
            EventSeverity.Information,
            "MappingConfigured",
            "EF Core mappings configured",
            """{"tables":15}""",
            now);
        var alert = new Alert(
            AlertId.New(),
            AlertSeverity.Warning,
            "persistence-tests",
            "Mapping coverage alert",
            "Inspect DbContext configuration",
            now,
            instanceId,
            repositoryId,
            runId,
            gitHubIssueNumber: 15);
        var report = new Report(
            ReportId.New(),
            ReportType.DailyDeliveryBrief,
            "platform",
            now.AddDays(-1),
            now,
            now,
            "# Daily",
            "<h1>Daily</h1>",
            pdfPath: null,
            """{"source":"test"}""");
        var auditEvent = new AuditEvent(
            AuditEventId.New(),
            "nickbeau",
            "ConfigureMappings",
            "PullRequest",
            "126",
            now,
            AuditEventOutcome.Succeeded,
            correlationId: "correlation-15",
            message: "Mappings configured",
            metadataJson: """{"issue":15}""");
        var operation = new BackgroundOperation(
            BackgroundOperationId.New(),
            "ConfigurePersistence",
            now,
            "Issue",
            "15",
            "correlation-15");

        alert.Acknowledge();
        operation.MarkRunning(now.AddMinutes(1));

        dbContext.AddRange(
            project,
            repository,
            secret,
            instance,
            workflowProfile,
            releaseArtifact,
            snapshot,
            issue,
            run,
            attempt,
            recordedEvent,
            alert,
            report,
            auditEvent,
            operation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        Repository savedRepository = await dbContext.Repositories.SingleAsync();
        SymphonyInstance savedInstance = await dbContext.SymphonyInstances.SingleAsync();
        InstanceSnapshot savedSnapshot = await dbContext.InstanceSnapshots.SingleAsync();
        SecretDescriptor savedSecret = await dbContext.SecretDescriptors.SingleAsync();
        Run savedRun = await dbContext.Runs.SingleAsync();
        Alert savedAlert = await dbContext.Alerts.SingleAsync();
        BackgroundOperation savedOperation = await dbContext.BackgroundOperations.SingleAsync();

        Assert.Equal(projectId, savedRepository.ProjectId);
        Assert.Equal("ReleasedGroup/TheConductor", savedRepository.FullName);
        Assert.Equal(RepositoryVisibility.Public, savedRepository.Visibility);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor.git"), savedRepository.CloneUrl);
        Assert.Equal(ExecutionMode.Docker, savedInstance.ExecutionMode);
        Assert.Equal(InstanceLifecycleStatus.Running, savedInstance.LifecycleStatus);
        Assert.Equal(secretId, savedInstance.GitHubCredentialSecretId);
        Assert.Equal(InstanceHealthStatus.Healthy, savedSnapshot.HealthStatus);
        Assert.Equal(140, savedSnapshot.TokenTotal);
        Assert.Equal("""{"running":1}""", savedSnapshot.StateJson);
        Assert.Equal(secretId, savedSecret.Id);
        Assert.Equal(SecretScopeType.Repository, savedSecret.ScopeType);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor/pull/126"), savedRun.PullRequestUrl);
        Assert.Equal(AlertStatus.Acknowledged, savedAlert.Status);
        Assert.Equal(BackgroundOperationStatus.Running, savedOperation.Status);
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
