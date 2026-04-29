using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Application.InstanceCollection;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Tests;

public sealed class CollectInstanceSnapshotServiceTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 4, 29, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CollectAsync_Persists_Health_Runtime_And_State_Payloads()
    {
        CollectableSymphonyInstance instance = CreateInstance();
        FakeSymphonyApiClient apiClient = new()
        {
            HealthResponse = new SymphonyHealthResponse(
                InstanceHealthStatus.Healthy,
                200,
                TimeSpan.FromMilliseconds(42.4),
                """{"status":"healthy"}"""),
            RuntimeResponse = new SymphonyRuntimeResponse("""{"version":"1.2.3"}"""),
            StateResponse = new SymphonyStateResponse("""{"sessions":[]}"""),
        };
        CapturingInstanceCollectionStore store = new();
        CollectInstanceSnapshotService service = new(
            apiClient,
            store,
            new FixedTimeProvider(CapturedAtUtc));

        InstanceCollectionResult result = await service.CollectAsync(
            instance,
            new InstanceCollectionRequest(IncludeRuntime: true, IncludeState: true),
            CancellationToken.None);

        Assert.Equal(InstanceHealthStatus.Healthy, result.HealthStatus);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(42, result.LatencyMilliseconds);
        Assert.Equal("""{"status":"healthy"}""", result.HealthJson);
        Assert.Equal("""{"version":"1.2.3"}""", result.RuntimeJson);
        Assert.Equal("""{"sessions":[]}""", result.StateJson);
        Assert.Null(result.ErrorMessage);
        Assert.Same(result, store.SavedResult);
    }

    [Fact]
    public async Task CollectAsync_Marks_Instance_Offline_When_Health_Poll_Fails()
    {
        CollectableSymphonyInstance instance = CreateInstance();
        FakeSymphonyApiClient apiClient = new()
        {
            HealthException = new HttpRequestException("connection refused"),
            RuntimeResponse = new SymphonyRuntimeResponse("""{"version":"1.2.3"}"""),
            StateResponse = new SymphonyStateResponse("""{"sessions":[]}"""),
        };
        CapturingInstanceCollectionStore store = new();
        CollectInstanceSnapshotService service = new(
            apiClient,
            store,
            new FixedTimeProvider(CapturedAtUtc));

        InstanceCollectionResult result = await service.CollectAsync(
            instance,
            new InstanceCollectionRequest(IncludeRuntime: true, IncludeState: true),
            CancellationToken.None);

        Assert.Equal(InstanceHealthStatus.Offline, result.HealthStatus);
        Assert.Contains("Health poll failed: connection refused", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(result.RuntimeJson);
        Assert.Null(result.StateJson);
        Assert.Equal(0, apiClient.RuntimeCallCount);
        Assert.Equal(0, apiClient.StateCallCount);
        Assert.Same(result, store.SavedResult);
    }

    private static CollectableSymphonyInstance CreateInstance() => new(
        SymphonyInstanceId.New(),
        RepositoryId.New(),
        "Primary",
        new Uri("http://localhost:5001"),
        InstanceLifecycleStatus.Running,
        InstanceHealthStatus.Unknown,
        LastHealthCheckAtUtc: null,
        LastSeenAtUtc: null);

    private sealed class CapturingInstanceCollectionStore : IInstanceCollectionStore
    {
        public InstanceCollectionResult? SavedResult { get; private set; }

        public Task<IReadOnlyList<CollectableSymphonyInstance>> ListCollectableInstancesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CollectableSymphonyInstance>>([]);

        public Task SaveCollectionResultAsync(
            InstanceCollectionResult result,
            CancellationToken cancellationToken)
        {
            SavedResult = result;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSymphonyApiClient : ISymphonyApiClient
    {
        public SymphonyHealthResponse? HealthResponse { get; init; }

        public SymphonyRuntimeResponse? RuntimeResponse { get; init; }

        public SymphonyStateResponse? StateResponse { get; init; }

        public Exception? HealthException { get; init; }

        public int RuntimeCallCount { get; private set; }

        public int StateCallCount { get; private set; }

        public Task<SymphonyHealthResponse> GetHealthAsync(
            Uri baseUri,
            CancellationToken cancellationToken)
        {
            if (HealthException is not null)
            {
                throw HealthException;
            }

            return Task.FromResult(HealthResponse ?? throw new InvalidOperationException("Health response not set."));
        }

        public Task<SymphonyRuntimeResponse> GetRuntimeAsync(
            Uri baseUri,
            CancellationToken cancellationToken)
        {
            RuntimeCallCount++;
            return Task.FromResult(RuntimeResponse ?? throw new InvalidOperationException("Runtime response not set."));
        }

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
            CancellationToken cancellationToken)
        {
            StateCallCount++;
            return Task.FromResult(StateResponse ?? throw new InvalidOperationException("State response not set."));
        }

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
