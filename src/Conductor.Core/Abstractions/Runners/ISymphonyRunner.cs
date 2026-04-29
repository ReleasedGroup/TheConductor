using Conductor.Core.Abstractions.Releases;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Abstractions.Runners;

public interface ISymphonyRunner
{
    ExecutionMode Mode { get; }

    Task<ProvisionedInstance> ProvisionAsync(
        SymphonyInstanceSpec spec,
        CancellationToken cancellationToken);

    Task StartAsync(SymphonyInstanceId instanceId, CancellationToken cancellationToken);

    Task StopAsync(SymphonyInstanceId instanceId, CancellationToken cancellationToken);

    Task RestartAsync(SymphonyInstanceId instanceId, CancellationToken cancellationToken);

    Task<InstanceHealthProbe> GetHealthAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken);

    Task<InstanceLogs> GetLogsAsync(
        SymphonyInstanceId instanceId,
        LogQuery query,
        CancellationToken cancellationToken);

    Task DestroyAsync(
        SymphonyInstanceId instanceId,
        DestroyInstanceOptions options,
        CancellationToken cancellationToken);
}

public sealed record SymphonyInstanceSpec(
    SymphonyInstanceId InstanceId,
    RepositoryId RepositoryId,
    string DisplayName,
    Uri BaseUrl,
    ReleaseSelector ReleaseSelector,
    int Port);

public sealed record ProvisionedInstance(
    SymphonyInstanceId InstanceId,
    Uri BaseUrl,
    InstanceLifecycleStatus LifecycleStatus);

public sealed record InstanceHealthProbe(
    InstanceHealthStatus Status,
    DateTimeOffset ObservedAtUtc,
    string? Message);

public sealed record InstanceLogs(
    string StandardOutput,
    string StandardError,
    DateTimeOffset CapturedAtUtc);

public sealed record LogQuery(DateTimeOffset? SinceUtc = null, int? TailLines = null);

public sealed record DestroyInstanceOptions(bool DeleteData);
