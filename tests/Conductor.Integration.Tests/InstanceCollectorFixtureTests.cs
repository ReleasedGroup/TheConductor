using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Application.InstanceCollection;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Integration.Tests;

public sealed class InstanceCollectorFixtureTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 4, 29, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Collector_Persists_Fixture_Based_Symphony_Payloads()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedInstanceAsync(dbContext);
        SqliteInstanceCollectionStore store = new(dbContext);
        IReadOnlyList<CollectableSymphonyInstance> instances =
            await store.ListCollectableInstancesAsync(CancellationToken.None);
        CollectableSymphonyInstance instance = Assert.Single(instances);
        FixtureSymphonyApiClient apiClient = new();
        CollectInstanceSnapshotService service = new(
            apiClient,
            store,
            new FixedTimeProvider(CapturedAtUtc));

        await service.CollectAsync(
            instance,
            new InstanceCollectionRequest(IncludeRuntime: true, IncludeState: true),
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(1, await dbContext.InstanceSnapshots
            .AsNoTracking()
            .CountAsync(snapshot =>
                snapshot.SymphonyInstanceId == fixture.InstanceId &&
                snapshot.HealthStatus == InstanceHealthStatus.Healthy));
        Assert.Equal(0, await dbContext.Alerts
            .AsNoTracking()
            .CountAsync(alert => alert.SymphonyInstanceId == fixture.InstanceId));

        string? stateJson = await dbContext.InstanceSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.SymphonyInstanceId == fixture.InstanceId)
            .Select(snapshot => snapshot.StateJson)
            .SingleAsync();

        Assert.Contains(
            "\"session-32\"",
            stateJson,
            StringComparison.Ordinal);
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

    private static async Task<InstanceFixture> SeedInstanceAsync(ConductorDbContext dbContext)
    {
        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();
        DateTimeOffset createdAtUtc = new(2026, 4, 29, 0, 0, 0, TimeSpan.Zero);

        dbContext.Projects.Add(new Project(
            projectId,
            "Platform",
            "Delivery",
            "Delivery tooling",
            "main",
            ProjectStatus.Active,
            createdAtUtc,
            createdAtUtc));
        dbContext.Repositories.Add(new Repository(
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
            createdAtUtc,
            RepositoryOrchestrationStatus.Eligible,
            orchestrationStatusReason: null));
        dbContext.SymphonyInstances.Add(new SymphonyInstance(
            instanceId,
            repositoryId,
            "Primary",
            ExecutionMode.LocalProcess,
            new Uri("http://localhost:5001"),
            createdAtUtc,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Unknown));
        await dbContext.SaveChangesAsync();

        return new InstanceFixture(repositoryId, instanceId);
    }

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Symphony", fileName));

    private sealed record InstanceFixture(
        RepositoryId RepositoryId,
        SymphonyInstanceId InstanceId);

    private sealed class FixtureSymphonyApiClient : ISymphonyApiClient
    {
        public Task<SymphonyHealthResponse> GetHealthAsync(
            Uri baseUri,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SymphonyHealthResponse(
                InstanceHealthStatus.Healthy,
                200,
                TimeSpan.FromMilliseconds(39),
                ReadFixture("health-healthy.json")));

        public Task<SymphonyRuntimeResponse> GetRuntimeAsync(
            Uri baseUri,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SymphonyRuntimeResponse(ReadFixture("runtime.json")));

        public Task<SymphonyWorkflowDocument> GetWorkflowAsync(
            Uri baseUri,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SymphonyWorkflowDocument> SaveWorkflowAsync(
            Uri baseUri,
            SymphonyWorkflowDocument document,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SymphonyStateResponse> GetStateAsync(
            Uri baseUri,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SymphonyStateResponse(ReadFixture("state.json")));

        public Task<SymphonyIssueResponse?> GetIssueAsync(
            Uri baseUri,
            string issueIdentifier,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SymphonyRefreshResponse> RequestRefreshAsync(
            Uri baseUri,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
