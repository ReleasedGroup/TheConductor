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

namespace Conductor.Persistence.Tests;

public sealed class SqliteInstanceCollectionStoreTests
{
    [Fact]
    public async Task SaveCollectionResultAsync_Stores_Snapshot_And_Raises_Offline_Event_And_Alert()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedInstanceAsync(dbContext);
        SqliteInstanceCollectionStore store = new(dbContext);
        DateTimeOffset capturedAtUtc = new(2026, 4, 29, 1, 0, 0, TimeSpan.Zero);

        await store.SaveCollectionResultAsync(
            new InstanceCollectionResult(
                fixture.InstanceId,
                fixture.RepositoryId,
                capturedAtUtc,
                InstanceHealthStatus.Offline,
                HttpStatusCode: null,
                LatencyMilliseconds: null,
                ErrorMessage: "Health poll failed: connection refused",
                HealthJson: null,
                RuntimeJson: null,
                StateJson: null),
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        SymphonyInstance instance = await dbContext.SymphonyInstances
            .AsNoTracking()
            .SingleAsync(instance => instance.Id == fixture.InstanceId);

        Assert.Equal(InstanceHealthStatus.Offline, instance.HealthStatus);
        Assert.Null(instance.LastSeenAtUtc);
        Assert.Equal(1, await dbContext.InstanceSnapshots
            .AsNoTracking()
            .CountAsync(snapshot => snapshot.SymphonyInstanceId == fixture.InstanceId));
        Assert.Equal(1, await dbContext.Events
            .AsNoTracking()
            .CountAsync(recordedEvent =>
                recordedEvent.SymphonyInstanceId == fixture.InstanceId &&
                recordedEvent.EventType == "InstanceHealthChanged"));
        Assert.Equal(1, await dbContext.Alerts
            .AsNoTracking()
            .CountAsync(alert =>
                alert.SymphonyInstanceId == fixture.InstanceId &&
                alert.Status == AlertStatus.Active &&
                alert.Source == "InstanceCollector" &&
                alert.Summary == "Symphony instance offline"));
    }

    [Fact]
    public async Task SaveCollectionResultAsync_Resolves_Offline_Alert_When_Instance_Recovers()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedInstanceAsync(dbContext);
        SqliteInstanceCollectionStore store = new(dbContext);
        DateTimeOffset offlineAtUtc = new(2026, 4, 29, 1, 0, 0, TimeSpan.Zero);
        DateTimeOffset healthyAtUtc = offlineAtUtc.AddMinutes(1);

        await store.SaveCollectionResultAsync(
            new InstanceCollectionResult(
                fixture.InstanceId,
                fixture.RepositoryId,
                offlineAtUtc,
                InstanceHealthStatus.Offline,
                HttpStatusCode: null,
                LatencyMilliseconds: null,
                ErrorMessage: "Health poll failed: connection refused",
                HealthJson: null,
                RuntimeJson: null,
                StateJson: null),
            CancellationToken.None);
        await store.SaveCollectionResultAsync(
            new InstanceCollectionResult(
                fixture.InstanceId,
                fixture.RepositoryId,
                healthyAtUtc,
                InstanceHealthStatus.Healthy,
                200,
                41,
                ErrorMessage: null,
                HealthJson: """{"status":"healthy"}""",
                RuntimeJson: """{"version":"1.2.3"}""",
                StateJson: """{"sessions":[]}"""),
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(0, await dbContext.Alerts
            .AsNoTracking()
            .CountAsync(alert =>
                alert.SymphonyInstanceId == fixture.InstanceId &&
                alert.Status == AlertStatus.Active));
        Assert.Equal(1, await dbContext.Alerts
            .AsNoTracking()
            .CountAsync(alert =>
                alert.SymphonyInstanceId == fixture.InstanceId &&
                alert.Status == AlertStatus.Resolved &&
                alert.ResolvedAtUtc != null));
        Assert.Equal(2, await dbContext.InstanceSnapshots
            .AsNoTracking()
            .CountAsync(snapshot => snapshot.SymphonyInstanceId == fixture.InstanceId));

        SymphonyInstance instance = await dbContext.SymphonyInstances
            .AsNoTracking()
            .SingleAsync(instance => instance.Id == fixture.InstanceId);

        Assert.NotNull(instance.LastSeenAtUtc);
    }

    [Fact]
    public async Task ListCollectableInstancesAsync_Excludes_Destroyed_Instances()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using ConductorDbContext dbContext = await CreateDbContextAsync(connection);
        var fixture = await SeedInstanceAsync(dbContext);
        SymphonyInstanceId destroyedInstanceId = SymphonyInstanceId.New();
        await AddInstanceAsync(
            dbContext,
            destroyedInstanceId,
            fixture.RepositoryId,
            "Retired",
            InstanceLifecycleStatus.Destroyed,
            InstanceHealthStatus.Offline);
        SqliteInstanceCollectionStore store = new(dbContext);

        IReadOnlyList<CollectableSymphonyInstance> instances =
            await store.ListCollectableInstancesAsync(CancellationToken.None);

        CollectableSymphonyInstance instance = Assert.Single(instances);
        Assert.Equal(fixture.InstanceId, instance.Id);
        Assert.Equal(new Uri("http://localhost:5001"), instance.BaseUrl);
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
        await AddInstanceAsync(
            dbContext,
            instanceId,
            repositoryId,
            "Primary",
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Unknown);

        return new InstanceFixture(repositoryId, instanceId);
    }

    private static async Task AddInstanceAsync(
        ConductorDbContext dbContext,
        SymphonyInstanceId instanceId,
        RepositoryId repositoryId,
        string displayName,
        InstanceLifecycleStatus lifecycleStatus,
        InstanceHealthStatus healthStatus)
    {
        DateTimeOffset createdAtUtc = new(2026, 4, 29, 0, 0, 0, TimeSpan.Zero);

        dbContext.SymphonyInstances.Add(new SymphonyInstance(
            instanceId,
            repositoryId,
            displayName,
            ExecutionMode.LocalProcess,
            new Uri("http://localhost:5001"),
            createdAtUtc,
            lifecycleStatus,
            healthStatus));
        await dbContext.SaveChangesAsync();
    }

    private sealed record InstanceFixture(
        RepositoryId RepositoryId,
        SymphonyInstanceId InstanceId);
}
