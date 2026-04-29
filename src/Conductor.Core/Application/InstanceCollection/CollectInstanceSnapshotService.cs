using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;

namespace Conductor.Core.Application.InstanceCollection;

public sealed class CollectInstanceSnapshotService
{
    private readonly ISymphonyApiClient symphonyApiClient;
    private readonly IInstanceCollectionStore collectionStore;
    private readonly TimeProvider timeProvider;

    public CollectInstanceSnapshotService(
        ISymphonyApiClient symphonyApiClient,
        IInstanceCollectionStore collectionStore,
        TimeProvider timeProvider)
    {
        this.symphonyApiClient = symphonyApiClient;
        this.collectionStore = collectionStore;
        this.timeProvider = timeProvider;
    }

    public async Task<InstanceCollectionResult> CollectAsync(
        CollectableSymphonyInstance instance,
        InstanceCollectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset capturedAtUtc = timeProvider.GetUtcNow();
        string? healthJson = null;
        string? runtimeJson = null;
        string? stateJson = null;
        int? httpStatusCode = null;
        long? latencyMilliseconds = null;
        InstanceHealthStatus healthStatus;
        List<string> errors = [];

        try
        {
            SymphonyHealthResponse health = await symphonyApiClient.GetHealthAsync(
                instance.BaseUrl,
                cancellationToken);

            healthStatus = health.Status;
            httpStatusCode = health.HttpStatusCode;
            latencyMilliseconds = ConvertLatency(health.Latency);
            healthJson = health.RawJson;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            healthStatus = InstanceHealthStatus.Offline;
            errors.Add($"Health poll failed: {exception.Message}");
        }

        if (healthStatus is not InstanceHealthStatus.Offline)
        {
            if (request.IncludeRuntime)
            {
                runtimeJson = await TryPollRuntimeAsync(instance.BaseUrl, errors, cancellationToken);
            }

            if (request.IncludeState)
            {
                stateJson = await TryPollStateAsync(instance.BaseUrl, errors, cancellationToken);
            }
        }

        InstanceCollectionResult result = new(
            instance.Id,
            instance.RepositoryId,
            capturedAtUtc,
            healthStatus,
            httpStatusCode,
            latencyMilliseconds,
            errors.Count == 0 ? null : string.Join(Environment.NewLine, errors),
            healthJson,
            runtimeJson,
            stateJson);

        await collectionStore.SaveCollectionResultAsync(result, cancellationToken);

        return result;
    }

    private async Task<string?> TryPollRuntimeAsync(
        Uri baseUrl,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            SymphonyRuntimeResponse runtime = await symphonyApiClient.GetRuntimeAsync(baseUrl, cancellationToken);
            return runtime.RawJson;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            errors.Add($"Runtime poll failed: {exception.Message}");
            return null;
        }
    }

    private async Task<string?> TryPollStateAsync(
        Uri baseUrl,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            SymphonyStateResponse state = await symphonyApiClient.GetStateAsync(baseUrl, cancellationToken);
            return state.RawJson;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            errors.Add($"State poll failed: {exception.Message}");
            return null;
        }
    }

    private static long ConvertLatency(TimeSpan latency)
    {
        double latencyMilliseconds = Math.Max(0, latency.TotalMilliseconds);
        return Convert.ToInt64(Math.Round(latencyMilliseconds, MidpointRounding.AwayFromZero));
    }
}
