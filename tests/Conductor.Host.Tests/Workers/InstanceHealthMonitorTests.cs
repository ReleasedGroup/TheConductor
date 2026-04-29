using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Conductor.Host.Workers;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DomainEvent = Conductor.Core.Domain.Events.Event;

namespace Conductor.Host.Tests.Workers;

public sealed class InstanceHealthMonitorTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
    private static readonly DateTimeOffset PolledAtUtc = DateTimeOffset.Parse("2026-04-29T01:05:00Z");

    [Fact]
    public async Task PollOnce_Creates_Offline_Event_And_Alert_When_Instance_Becomes_Unreachable()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<ConductorDbContext> options = CreateOptions(connection);
        await using ConductorDbContext dbContext = await CreateDbContextAsync(options);

        SymphonyInstanceId instanceId = await SeedInstanceAsync(
            dbContext,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy);
        var apiClient = new FakeSymphonyApiClient(
            new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                HttpStatusCode: null,
                TimeSpan.FromMilliseconds(120),
                """{"kind":"request_failed"}"""));
        InstanceHealthMonitor monitor = CreateMonitor(dbContext, apiClient);

        int polledCount = await monitor.PollOnceAsync();

        SymphonyInstance savedInstance = await dbContext.SymphonyInstances.SingleAsync();
        DomainEvent recordedEvent = await dbContext.Events.SingleAsync();
        Alert alert = await dbContext.Alerts.SingleAsync();

        Assert.Equal(1, polledCount);
        Assert.Equal(instanceId, savedInstance.Id);
        Assert.Equal(InstanceHealthStatus.Offline, savedInstance.HealthStatus);
        Assert.Equal(PolledAtUtc, savedInstance.LastHealthCheckAtUtc);
        Assert.Null(savedInstance.LastSeenAtUtc);
        Assert.Equal(InstanceHealthStatus.Offline, await dbContext.InstanceSnapshots
            .Select(snapshot => snapshot.HealthStatus)
            .SingleAsync());
        Assert.Equal(InstanceHealthMonitor.OfflineEventType, recordedEvent.EventType);
        Assert.Equal(EventSeverity.Critical, recordedEvent.Severity);
        Assert.Contains("became unreachable", recordedEvent.Message, StringComparison.Ordinal);
        Assert.Equal(InstanceHealthMonitor.OfflineAlertSource, alert.Source);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(AlertStatus.Active, alert.Status);
        Assert.Equal(instanceId, alert.SymphonyInstanceId);
    }

    [Fact]
    public async Task PollOnce_Does_Not_Duplicate_Unresolved_Offline_Alert()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<ConductorDbContext> options = CreateOptions(connection);
        await using ConductorDbContext dbContext = await CreateDbContextAsync(options);

        await SeedInstanceAsync(
            dbContext,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy);
        var apiClient = new FakeSymphonyApiClient(
            new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                HttpStatusCode: null,
                TimeSpan.FromMilliseconds(120),
                """{"kind":"request_failed"}"""),
            new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                HttpStatusCode: null,
                TimeSpan.FromMilliseconds(90),
                """{"kind":"request_failed"}"""));
        InstanceHealthMonitor monitor = CreateMonitor(dbContext, apiClient);

        await monitor.PollOnceAsync();
        await monitor.PollOnceAsync();

        Assert.Equal(2, await dbContext.InstanceSnapshots.CountAsync());
        Assert.Equal(1, await dbContext.Events.CountAsync());
        Assert.Equal(1, await dbContext.Alerts.CountAsync());
    }

    [Fact]
    public async Task PollOnce_Continues_When_One_Instance_Health_Request_Fails()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<ConductorDbContext> options = CreateOptions(connection);
        await using ConductorDbContext dbContext = await CreateDbContextAsync(options);

        SymphonyInstanceId unreachableInstanceId = await SeedInstanceAsync(
            dbContext,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy,
            displayName: "A Unreachable instance",
            repositoryOwner: "ReleasedGroup",
            repositoryName: "Unreachable");
        SymphonyInstanceId healthyInstanceId = await SeedInstanceAsync(
            dbContext,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Unknown,
            displayName: "B Healthy instance",
            repositoryOwner: "ReleasedGroup",
            repositoryName: "Healthy");
        var apiClient = new FakeSymphonyApiClient(
            new HttpRequestException("connection refused"),
            new SymphonyHealthResponse(
                InstanceHealthStatus.Healthy,
                200,
                TimeSpan.FromMilliseconds(18),
                """{"status":"healthy"}"""));
        InstanceHealthMonitor monitor = CreateMonitor(dbContext, apiClient);

        int polledCount = await monitor.PollOnceAsync();

        Dictionary<SymphonyInstanceId, InstanceHealthStatus> statuses = await dbContext.SymphonyInstances
            .ToDictionaryAsync(instance => instance.Id, instance => instance.HealthStatus);

        Assert.Equal(2, polledCount);
        Assert.Equal(InstanceHealthStatus.Offline, statuses[unreachableInstanceId]);
        Assert.Equal(InstanceHealthStatus.Healthy, statuses[healthyInstanceId]);
        Assert.Equal(2, await dbContext.InstanceSnapshots.CountAsync());
        Assert.Equal(1, await dbContext.Events.CountAsync());
        Assert.Equal(1, await dbContext.Alerts.CountAsync());
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static DbContextOptions<ConductorDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

    private static async Task<ConductorDbContext> CreateDbContextAsync(
        DbContextOptions<ConductorDbContext> options)
    {
        var dbContext = new ConductorDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static InstanceHealthMonitor CreateMonitor(
        ConductorDbContext dbContext,
        ISymphonyApiClient apiClient) =>
        new(
            dbContext,
            apiClient,
            new FixedTimeProvider(PolledAtUtc),
            NullLogger<InstanceHealthMonitor>.Instance);

    private static async Task<SymphonyInstanceId> SeedInstanceAsync(
        ConductorDbContext dbContext,
        InstanceLifecycleStatus lifecycleStatus,
        InstanceHealthStatus healthStatus,
        string displayName = "TheConductor main",
        string repositoryOwner = "ReleasedGroup",
        string repositoryName = "TheConductor")
    {
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();

        dbContext.Repositories.Add(new Repository(
            repositoryId,
            RepositoryProvider.GitHub,
            repositoryOwner,
            repositoryName,
            "main",
            new Uri($"https://github.com/{repositoryOwner}/{repositoryName}.git"),
            new Uri($"https://github.com/{repositoryOwner}/{repositoryName}"),
            RepositoryVisibility.Public,
            isArchived: false,
            projectId: null,
            lastSyncedAtUtc: CreatedAtUtc,
            RepositoryOrchestrationStatus.Eligible,
            orchestrationStatusReason: null));
        dbContext.SymphonyInstances.Add(new SymphonyInstance(
            instanceId,
            repositoryId,
            displayName,
            ExecutionMode.Docker,
            new Uri($"http://localhost:{Random.Shared.Next(10000, 20000)}"),
            CreatedAtUtc,
            lifecycleStatus,
            healthStatus));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return instanceId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeSymphonyApiClient : ISymphonyApiClient
    {
        private readonly Queue<object> healthResults;

        public FakeSymphonyApiClient(params object[] healthResults)
        {
            this.healthResults = new Queue<object>(healthResults);
        }

        public Task<SymphonyHealthResponse> GetHealthAsync(
            Uri baseUri,
            CancellationToken cancellationToken)
        {
            object result = healthResults.Dequeue();

            if (result is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((SymphonyHealthResponse)result);
        }

        public Task<SymphonyRuntimeResponse> GetRuntimeAsync(
            Uri baseUri,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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
            throw new NotSupportedException();

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
}
