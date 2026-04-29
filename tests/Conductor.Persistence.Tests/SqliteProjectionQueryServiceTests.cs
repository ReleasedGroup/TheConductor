using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
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
        Assert.Equal(3, dashboard.Metrics.OpenPullRequestCount);
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
            pullRequestCount: 2,
            createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            webRepositoryId,
            projectAlphaId,
            "web-client",
            isArchived: false,
            pullRequestCount: 1,
            createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            archivedRepositoryId,
            projectAlphaId,
            "api-archive",
            isArchived: true,
            pullRequestCount: 8,
            createdAtUtc);
        await InsertRepositoryAsync(
            dbContext,
            betaRepositoryId,
            projectBetaId,
            "api-beta",
            isArchived: false,
            pullRequestCount: 5,
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
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Projects (Id, Name, OwnerName, Status, CreatedAtUtc, UpdatedAtUtc)
            VALUES ({FormatId(projectId.Value)}, {name}, {"Delivery"}, {nameof(ProjectStatus.Active)}, {createdAtUtc}, {createdAtUtc});
            """);
    }

    private static async Task InsertRepositoryAsync(
        ConductorDbContext dbContext,
        RepositoryId repositoryId,
        ProjectId projectId,
        string name,
        bool isArchived,
        int pullRequestCount,
        DateTimeOffset createdAtUtc)
    {
        string fullName = $"https://github.com/ReleasedGroup/{name}";

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Repositories
                (Id, ProjectId, Provider, Owner, Name, DefaultBranch, CloneUrl, WebUrl, IsArchived, OpenIssueCount, PullRequestCount, ImportedAtUtc, UpdatedAtUtc)
            VALUES
                ({FormatId(repositoryId.Value)}, {FormatId(projectId.Value)}, {nameof(RepositoryProvider.GitHub)}, {"ReleasedGroup"}, {name}, {"main"}, {fullName + ".git"}, {fullName}, {isArchived}, {0}, {pullRequestCount}, {createdAtUtc}, {createdAtUtc});
            """);
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
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SymphonyInstances
                (Id, RepositoryId, WorkflowProfileId, DisplayName, ExecutionMode, BaseUrl, Status, HealthStatus, DeliveryStatus, ReleaseSelector, ResolvedReleaseTag, GitHubSecretId, OpenAiSecretId, CreatedAtUtc, UpdatedAtUtc, LastHealthCheckAtUtc, LastSeenAtUtc)
            VALUES
                ({FormatId(instanceId.Value)}, {FormatId(repositoryId.Value)}, {null}, {displayName}, {executionMode.ToString()}, {baseUrl}, {lifecycleStatus.ToString()}, {healthStatus.ToString()}, {"Healthy"}, {null}, {null}, {null}, {null}, {createdAtUtc}, {createdAtUtc}, {lastHealthCheckAtUtc}, {lastSeenAtUtc});
            """);
    }

    private static async Task InsertSnapshotAsync(
        ConductorDbContext dbContext,
        SymphonyInstanceId instanceId,
        DateTimeOffset capturedAtUtc)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO InstanceSnapshots
                (Id, SymphonyInstanceId, CapturedAtUtc, HealthStatus, HttpStatusCode, LatencyMilliseconds, ErrorMessage, HealthJson, RuntimeJson, StateJson)
            VALUES
                ({FormatId(Guid.NewGuid())}, {FormatId(instanceId.Value)}, {capturedAtUtc}, {nameof(InstanceHealthStatus.Healthy)}, {200}, {42L}, {null}, {"""{"status":"healthy"}"""}, {"{}"}, {"{}"});
            """);
    }

    private static async Task InsertTrackedIssueAsync(
        ConductorDbContext dbContext,
        RepositoryId repositoryId,
        bool isBlocked,
        DateTimeOffset updatedAtUtc)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TrackedIssues
                (Id, RepositoryId, GitHubIssueNumber, Title, SymphonyStatus, IsBlocked, Url, UpdatedAtUtc, LabelsJson, AssigneesJson, PullRequestsJson)
            VALUES
                ({FormatId(Guid.NewGuid())}, {FormatId(repositoryId.Value)}, {18L}, {"Issue"}, {nameof(SymphonyIssueStatus.Running)}, {isBlocked}, {null}, {updatedAtUtc}, {null}, {null}, {null});
            """);
    }

    private static string FormatId(Guid id) => id.ToString("D");

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
