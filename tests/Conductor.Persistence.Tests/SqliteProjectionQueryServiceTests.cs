using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Persistence.Tests;

public sealed class SqliteProjectionQueryServiceTests
{
    [Fact]
    public async Task ListRepositoriesAsync_Filters_By_Project_And_Search_And_Excludes_Archived()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        IReadOnlyList<RepositoryListItemProjection> repositories =
            await queryService.ListRepositoriesAsync(new RepositoryListQuery(
                fixture.ProjectAlphaId,
                Search: "api"));

        RepositoryListItemProjection repository = Assert.Single(repositories);
        Assert.Equal(fixture.ApiRepositoryId, repository.Id);
        Assert.Equal("Alpha", repository.ProjectName);
        Assert.Equal("ReleasedGroup/api-service", repository.FullName);
        Assert.Equal(2, repository.InstanceCount);
        Assert.Equal(1, repository.RunningInstanceCount);
        Assert.Equal(InstanceHealthStatus.Warning, repository.WorstHealthStatus);
        Assert.Equal(fixture.WarningObservedAtUtc, repository.LastHealthCheckAtUtc);
    }

    [Fact]
    public async Task ListInstanceSummariesAsync_Returns_Latest_Snapshot_And_Excludes_Destroyed_Instances()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        IReadOnlyList<InstanceSummaryProjection> instances =
            await queryService.ListInstanceSummariesAsync(new InstanceSummaryQuery(
                RepositoryId: fixture.ApiRepositoryId));

        Assert.Equal(2, instances.Count);

        InstanceSummaryProjection runningInstance = Assert.Single(instances, instance =>
            instance.Id == fixture.ApiPrimaryInstanceId);
        Assert.Equal("ReleasedGroup/api-service", runningInstance.RepositoryFullName);
        Assert.Equal("Alpha", runningInstance.ProjectName);
        Assert.Equal(InstanceLifecycleStatus.Running, runningInstance.LifecycleStatus);
        Assert.Equal(fixture.LatestSnapshotAtUtc, runningInstance.LatestSnapshotCapturedAtUtc);
        Assert.Equal("1.2.3", runningInstance.SymphonyVersion);
        Assert.Equal("ReleasedGroup", runningInstance.WorkflowOwner);
        Assert.Equal("api-service", runningInstance.WorkflowRepository);
        Assert.Equal("/config/api-service/WORKFLOW.md", runningInstance.WorkflowSourcePath);
        Assert.Equal(1, runningInstance.ActiveIssueCount);
        Assert.Equal(1, runningInstance.RunningSessionCount);
        Assert.Equal(0, runningInstance.RetryQueueCount);
        Assert.Equal(140, runningInstance.TokenTotal);

        Assert.DoesNotContain(instances, instance => instance.Id == fixture.DestroyedInstanceId);
    }

    [Fact]
    public async Task ListRepositoriesAsync_Uses_Unknown_Health_For_Repositories_Without_Instances()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        IReadOnlyList<RepositoryListItemProjection> repositories =
            await queryService.ListRepositoriesAsync(new RepositoryListQuery(fixture.ProjectBetaId));

        RepositoryListItemProjection repository = Assert.Single(repositories);
        Assert.Equal(fixture.BetaRepositoryId, repository.Id);
        Assert.Equal(0, repository.InstanceCount);
        Assert.Equal(InstanceHealthStatus.Unknown, repository.WorstHealthStatus);
        Assert.Null(repository.LastHealthCheckAtUtc);
    }

    [Fact]
    public async Task GetDashboardAsync_Returns_Project_Scoped_Fleet_Projection()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        DashboardProjection dashboard =
            await queryService.GetDashboardAsync(new DashboardQuery(fixture.ProjectAlphaId));

        Assert.Equal(2, dashboard.Metrics.ManagedRepositoryCount);
        Assert.Equal(1, dashboard.Metrics.HealthyRepositoryCount);
        Assert.Equal(1, dashboard.Metrics.ActiveAgentCount);
        Assert.Equal(1, dashboard.Metrics.BlockedIssueCount);
        Assert.Equal(0, dashboard.Metrics.OpenPullRequestCount);
        Assert.Equal(0m, dashboard.Metrics.EstimatedSpendToday);
        Assert.Equal(2, dashboard.ActiveRepositories.Count);
        Assert.Equal(3, dashboard.InstanceSummaries.Count);
        Assert.Equal(2, dashboard.HealthBuckets.Single(bucket =>
            bucket.Status == InstanceHealthStatus.Healthy).Count);
        Assert.Equal(1, dashboard.HealthBuckets.Single(bucket =>
            bucket.Status == InstanceHealthStatus.Warning).Count);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        return connection;
    }

    private static async Task<ConductorDbContext> CreateDbContextAsync(SqliteConnection connection)
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        ConductorDbContext dbContext = new(options);
        await dbContext.Database.MigrateAsync();

        return dbContext;
    }

    private static async Task<PortfolioFixture> SeedPortfolioAsync(ConductorDbContext dbContext)
    {
        DateTimeOffset createdAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        DateTimeOffset healthyObservedAtUtc = createdAtUtc.AddMinutes(5);
        DateTimeOffset warningObservedAtUtc = createdAtUtc.AddMinutes(10);
        DateTimeOffset latestSnapshotAtUtc = createdAtUtc.AddMinutes(15);

        ProjectId projectAlphaId = ProjectId.New();
        ProjectId projectBetaId = ProjectId.New();
        RepositoryId apiRepositoryId = RepositoryId.New();
        RepositoryId webRepositoryId = RepositoryId.New();
        RepositoryId archivedRepositoryId = RepositoryId.New();
        RepositoryId betaRepositoryId = RepositoryId.New();
        SymphonyInstanceId apiPrimaryInstanceId = SymphonyInstanceId.New();
        SymphonyInstanceId apiCanaryInstanceId = SymphonyInstanceId.New();
        SymphonyInstanceId webInstanceId = SymphonyInstanceId.New();
        SymphonyInstanceId destroyedInstanceId = SymphonyInstanceId.New();

        await InsertProjectAsync(dbContext, projectAlphaId, "Alpha", createdAtUtc);
        await InsertProjectAsync(dbContext, projectBetaId, "Beta", createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            apiRepositoryId,
            projectAlphaId,
            "api-service",
            isArchived: false,
            createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            webRepositoryId,
            projectAlphaId,
            "web-client",
            isArchived: false,
            createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            archivedRepositoryId,
            projectAlphaId,
            "api-archive",
            isArchived: true,
            createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            betaRepositoryId,
            projectBetaId,
            "api-beta",
            isArchived: false,
            createdAtUtc);

        await InsertInstanceAsync(
            dbContext,
            apiPrimaryInstanceId,
            apiRepositoryId,
            "API primary",
            ExecutionMode.Docker,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy,
            "http://localhost:5010",
            createdAtUtc,
            healthyObservedAtUtc,
            healthyObservedAtUtc);
        await InsertInstanceAsync(
            dbContext,
            apiCanaryInstanceId,
            apiRepositoryId,
            "API canary",
            ExecutionMode.LocalProcess,
            InstanceLifecycleStatus.Stopped,
            InstanceHealthStatus.Warning,
            "http://localhost:5011",
            createdAtUtc,
            warningObservedAtUtc,
            warningObservedAtUtc);
        await InsertInstanceAsync(
            dbContext,
            webInstanceId,
            webRepositoryId,
            "Web primary",
            ExecutionMode.Docker,
            InstanceLifecycleStatus.Stopped,
            InstanceHealthStatus.Healthy,
            "http://localhost:5020",
            createdAtUtc,
            healthyObservedAtUtc,
            healthyObservedAtUtc);
        await InsertInstanceAsync(
            dbContext,
            destroyedInstanceId,
            apiRepositoryId,
            "API retired",
            ExecutionMode.Docker,
            InstanceLifecycleStatus.Destroyed,
            InstanceHealthStatus.Offline,
            "http://localhost:5099",
            createdAtUtc,
            warningObservedAtUtc,
            null);

        await InsertSnapshotAsync(dbContext, apiPrimaryInstanceId, healthyObservedAtUtc);
        await InsertSnapshotAsync(dbContext, apiPrimaryInstanceId, latestSnapshotAtUtc);
        await InsertTrackedIssueAsync(dbContext, apiRepositoryId, isBlocked: true, createdAtUtc);
        await InsertTrackedIssueAsync(dbContext, webRepositoryId, isBlocked: false, createdAtUtc);

        dbContext.ChangeTracker.Clear();

        return new PortfolioFixture(
            projectAlphaId,
            projectBetaId,
            apiRepositoryId,
            betaRepositoryId,
            apiPrimaryInstanceId,
            destroyedInstanceId,
            warningObservedAtUtc,
            latestSnapshotAtUtc);
    }

    private static async Task InsertProjectAsync(
        ConductorDbContext dbContext,
        ProjectId projectId,
        string name,
        DateTimeOffset createdAtUtc)
    {
        dbContext.Projects.Add(new Project(
            projectId,
            name,
            "Delivery",
            "Dashboard seed data",
            "main",
            ProjectStatus.Active,
            createdAtUtc,
            createdAtUtc));

        await dbContext.SaveChangesAsync();
    }

    private static async Task InsertRepositoryAsync(
        ConductorDbContext dbContext,
        RepositoryId repositoryId,
        ProjectId projectId,
        string name,
        bool isArchived,
        DateTimeOffset createdAtUtc)
    {
        var repositoryStatus = isArchived
            ? RepositoryOrchestrationStatus.Ineligible
            : RepositoryOrchestrationStatus.Eligible;
        string? repositoryStatusReason = isArchived
            ? "Archived repositories cannot be orchestrated."
            : null;

        dbContext.Repositories.Add(new Repository(
            repositoryId,
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            name,
            "main",
            new Uri($"https://github.com/ReleasedGroup/{name}.git"),
            new Uri($"https://github.com/ReleasedGroup/{name}"),
            RepositoryVisibility.Public,
            isArchived,
            projectId,
            createdAtUtc,
            repositoryStatus,
            repositoryStatusReason));

        await dbContext.SaveChangesAsync();
    }

    private static async Task InsertInstanceAsync(
        ConductorDbContext dbContext,
        SymphonyInstanceId instanceId,
        RepositoryId repositoryId,
        string displayName,
        ExecutionMode executionMode,
        InstanceLifecycleStatus lifecycleStatus,
        InstanceHealthStatus healthStatus,
        string baseUrl,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? lastHealthCheckAtUtc,
        DateTimeOffset? lastSeenAtUtc)
    {
        var instance = new SymphonyInstance(
            instanceId,
            repositoryId,
            displayName,
            executionMode,
            new Uri(baseUrl),
            createdAtUtc,
            lifecycleStatus,
            healthStatus,
            lastSeenAtUtc: lastSeenAtUtc);

        if (lastHealthCheckAtUtc is { } observedAtUtc)
        {
            instance.RecordHealth(healthStatus, observedAtUtc);
        }

        dbContext.SymphonyInstances.Add(instance);
        await dbContext.SaveChangesAsync();
    }

    private static async Task InsertSnapshotAsync(
        ConductorDbContext dbContext,
        SymphonyInstanceId instanceId,
        DateTimeOffset capturedAtUtc)
    {
        dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
            InstanceSnapshotId.New(),
            instanceId,
            capturedAtUtc,
            InstanceHealthStatus.Healthy,
            """{"status":"healthy"}""",
            """
            {
              "applicationName": "Symphony",
              "version": "1.2.3",
              "workflow": {
                "owner": "ReleasedGroup",
                "repository": "api-service",
                "sourcePath": "/config/api-service/WORKFLOW.md"
              }
            }
            """,
            "{}",
            activeIssueCount: 1,
            runningSessionCount: 1,
            retryQueueCount: 0,
            failedRunCount: 0,
            tokenInputTotal: 100,
            tokenOutputTotal: 40));

        await dbContext.SaveChangesAsync();
    }

    private static async Task InsertTrackedIssueAsync(
        ConductorDbContext dbContext,
        RepositoryId repositoryId,
        bool isBlocked,
        DateTimeOffset updatedAtUtc)
    {
        dbContext.TrackedIssues.Add(new TrackedIssue(
            id: TrackedIssueId.New(),
            repositoryId: repositoryId,
            gitHubIssueNumber: 18,
            title: "Issue",
            state: TrackedIssueState.Open,
            labelsJson: null,
            milestone: null,
            assigneeLoginsJson: null,
            url: new Uri("https://github.com/ReleasedGroup/TheConductor/issues/18"),
            symphonyStatus: SymphonyIssueStatus.Running,
            lastRunStatus: null,
            lastActivityAtUtc: updatedAtUtc,
            isBlocked: isBlocked,
            blockerReason: isBlocked ? "Waiting on review" : null));

        await dbContext.SaveChangesAsync();
    }

    private sealed record PortfolioFixture(
        ProjectId ProjectAlphaId,
        ProjectId ProjectBetaId,
        RepositoryId ApiRepositoryId,
        RepositoryId BetaRepositoryId,
        SymphonyInstanceId ApiPrimaryInstanceId,
        SymphonyInstanceId DestroyedInstanceId,
        DateTimeOffset WarningObservedAtUtc,
        DateTimeOffset LatestSnapshotAtUtc);
}
