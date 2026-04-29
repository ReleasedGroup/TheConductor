using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Persistence.Tests;

public sealed class SqliteProjectionQueryServiceTests
{
    [Fact]
    public async Task ListRepositoriesAsync_Filters_By_Project_And_Search_And_Excludes_Archived()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        IReadOnlyList<RepositoryListItemProjection> repositories =
            await queryService.ListRepositoriesAsync(new RepositoryListQuery(
                fixture.ProjectAlpha.Id,
                Search: "api"));

        RepositoryListItemProjection repository = Assert.Single(repositories);
        Assert.Equal(fixture.ApiRepository.Id, repository.Id);
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
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        IReadOnlyList<InstanceSummaryProjection> instances =
            await queryService.ListInstanceSummariesAsync(new InstanceSummaryQuery(
                RepositoryId: fixture.ApiRepository.Id));

        Assert.Equal(2, instances.Count);

        InstanceSummaryProjection runningInstance = Assert.Single(instances, instance =>
            instance.Id == fixture.ApiPrimaryInstance.Id);
        Assert.Equal("ReleasedGroup/api-service", runningInstance.RepositoryFullName);
        Assert.Equal("Alpha", runningInstance.ProjectName);
        Assert.Equal(InstanceLifecycleStatus.Running, runningInstance.LifecycleStatus);
        Assert.Equal(fixture.LatestSnapshotAtUtc, runningInstance.LatestSnapshotCapturedAtUtc);

        Assert.DoesNotContain(instances, instance => instance.Id == fixture.DestroyedInstance.Id);
    }

    [Fact]
    public async Task ListRepositoriesAsync_Uses_Unknown_Health_For_Repositories_Without_Instances()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        IReadOnlyList<RepositoryListItemProjection> repositories =
            await queryService.ListRepositoriesAsync(new RepositoryListQuery(fixture.ProjectBeta.Id));

        RepositoryListItemProjection repository = Assert.Single(repositories);
        Assert.Equal(fixture.BetaRepository.Id, repository.Id);
        Assert.Equal(0, repository.InstanceCount);
        Assert.Equal(InstanceHealthStatus.Unknown, repository.WorstHealthStatus);
        Assert.Null(repository.LastHealthCheckAtUtc);
    }

    [Fact]
    public async Task GetDashboardAsync_Returns_Project_Scoped_Fleet_Projection()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        var fixture = await SeedPortfolioAsync(dbContext);
        SqliteProjectionQueryService queryService = new(dbContext);

        DashboardProjection dashboard =
            await queryService.GetDashboardAsync(new DashboardQuery(fixture.ProjectAlpha.Id));

        Assert.Equal(2, dashboard.Metrics.ManagedRepositoryCount);
        Assert.Equal(1, dashboard.Metrics.HealthyRepositoryCount);
        Assert.Equal(1, dashboard.Metrics.ActiveAgentCount);
        Assert.Equal(0, dashboard.Metrics.BlockedIssueCount);
        Assert.Equal(0, dashboard.Metrics.OpenPullRequestCount);
        Assert.Equal(0m, dashboard.Metrics.EstimatedSpendToday);
        Assert.Equal(2, dashboard.ActiveRepositories.Count);
        Assert.Equal(3, dashboard.InstanceSummaries.Count);
        Assert.Equal(2, dashboard.HealthBuckets.Single(bucket =>
            bucket.Status == InstanceHealthStatus.Healthy).Count);
        Assert.Equal(1, dashboard.HealthBuckets.Single(bucket =>
            bucket.Status == InstanceHealthStatus.Warning).Count);
    }

    private static async Task<ConductorDbContext> CreateDbContextAsync()
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        ConductorDbContext dbContext = new(options);
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }

    private static async Task<PortfolioFixture> SeedPortfolioAsync(ConductorDbContext dbContext)
    {
        DateTimeOffset createdAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        DateTimeOffset healthyObservedAtUtc = createdAtUtc.AddMinutes(5);
        DateTimeOffset warningObservedAtUtc = createdAtUtc.AddMinutes(10);
        DateTimeOffset latestSnapshotAtUtc = createdAtUtc.AddMinutes(15);

        var projectAlpha = new Project(
            ProjectId.New(),
            "Alpha",
            "Delivery",
            ProjectStatus.Active,
            createdAtUtc,
            createdAtUtc);
        var projectBeta = new Project(
            ProjectId.New(),
            "Beta",
            "Delivery",
            ProjectStatus.Active,
            createdAtUtc,
            createdAtUtc);

        var apiRepository = new Repository(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "api-service",
            "main",
            new Uri("https://github.com/ReleasedGroup/api-service.git"),
            new Uri("https://github.com/ReleasedGroup/api-service"),
            isArchived: false,
            projectAlpha.Id);
        var webRepository = new Repository(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "web-client",
            "main",
            new Uri("https://github.com/ReleasedGroup/web-client.git"),
            new Uri("https://github.com/ReleasedGroup/web-client"),
            isArchived: false,
            projectAlpha.Id);
        var archivedRepository = new Repository(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "api-archive",
            "main",
            new Uri("https://github.com/ReleasedGroup/api-archive.git"),
            new Uri("https://github.com/ReleasedGroup/api-archive"),
            isArchived: true,
            projectAlpha.Id);
        var betaRepository = new Repository(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "api-beta",
            "main",
            new Uri("https://github.com/ReleasedGroup/api-beta.git"),
            new Uri("https://github.com/ReleasedGroup/api-beta"),
            isArchived: false,
            projectBeta.Id);

        var apiPrimaryInstance = new SymphonyInstance(
            SymphonyInstanceId.New(),
            apiRepository.Id,
            "API primary",
            ExecutionMode.Docker,
            new Uri("http://localhost:5010"),
            InstanceLifecycleStatus.Running);
        apiPrimaryInstance.RecordHealth(InstanceHealthStatus.Healthy, healthyObservedAtUtc);

        var apiCanaryInstance = new SymphonyInstance(
            SymphonyInstanceId.New(),
            apiRepository.Id,
            "API canary",
            ExecutionMode.LocalProcess,
            new Uri("http://localhost:5011"),
            InstanceLifecycleStatus.Stopped);
        apiCanaryInstance.RecordHealth(InstanceHealthStatus.Warning, warningObservedAtUtc);

        var webInstance = new SymphonyInstance(
            SymphonyInstanceId.New(),
            webRepository.Id,
            "Web primary",
            ExecutionMode.Docker,
            new Uri("http://localhost:5020"),
            InstanceLifecycleStatus.Stopped);
        webInstance.RecordHealth(InstanceHealthStatus.Healthy, healthyObservedAtUtc);

        var destroyedInstance = new SymphonyInstance(
            SymphonyInstanceId.New(),
            apiRepository.Id,
            "API retired",
            ExecutionMode.Docker,
            new Uri("http://localhost:5099"),
            InstanceLifecycleStatus.Destroyed);
        destroyedInstance.RecordHealth(InstanceHealthStatus.Offline, warningObservedAtUtc);

        dbContext.AddRange(projectAlpha, projectBeta);
        dbContext.AddRange(apiRepository, webRepository, archivedRepository, betaRepository);
        dbContext.AddRange(apiPrimaryInstance, apiCanaryInstance, webInstance, destroyedInstance);
        dbContext.AddRange(
            new InstanceSnapshot(
                apiPrimaryInstance.Id,
                healthyObservedAtUtc,
                InstanceHealthStatus.Healthy,
                """{"status":"healthy"}""",
                "{}",
                "{}"),
            new InstanceSnapshot(
                apiPrimaryInstance.Id,
                latestSnapshotAtUtc,
                InstanceHealthStatus.Healthy,
                """{"status":"healthy"}""",
                "{}",
                "{}"));

        await dbContext.SaveChangesAsync();

        return new PortfolioFixture(
            projectAlpha,
            projectBeta,
            apiRepository,
            betaRepository,
            apiPrimaryInstance,
            destroyedInstance,
            warningObservedAtUtc,
            latestSnapshotAtUtc);
    }

    private sealed record PortfolioFixture(
        Project ProjectAlpha,
        Project ProjectBeta,
        Repository ApiRepository,
        Repository BetaRepository,
        SymphonyInstance ApiPrimaryInstance,
        SymphonyInstance DestroyedInstance,
        DateTimeOffset WarningObservedAtUtc,
        DateTimeOffset LatestSnapshotAtUtc);
}
