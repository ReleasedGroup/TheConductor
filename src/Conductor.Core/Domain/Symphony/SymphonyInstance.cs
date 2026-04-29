using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Symphony;

public sealed class SymphonyInstance
{
    public SymphonyInstance(
        SymphonyInstanceId id,
        RepositoryId repositoryId,
        string displayName,
        ExecutionMode executionMode,
        Uri baseUrl,
        InstanceLifecycleStatus lifecycleStatus = InstanceLifecycleStatus.NotProvisioned,
        InstanceHealthStatus healthStatus = InstanceHealthStatus.Unknown)
    {
        Id = id;
        RepositoryId = repositoryId;
        DisplayName = Guard.NotWhiteSpace(displayName, nameof(displayName));
        ExecutionMode = executionMode;
        BaseUrl = Guard.AbsoluteUri(baseUrl, nameof(baseUrl));
        LifecycleStatus = lifecycleStatus;
        HealthStatus = healthStatus;
    }

    public SymphonyInstanceId Id { get; }

    public RepositoryId RepositoryId { get; }

    public string DisplayName { get; }

    public ExecutionMode ExecutionMode { get; }

    public Uri BaseUrl { get; }

    public InstanceLifecycleStatus LifecycleStatus { get; private set; }

    public InstanceHealthStatus HealthStatus { get; private set; }

    public DateTimeOffset? LastHealthCheckAtUtc { get; private set; }

    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    public void MarkLifecycle(InstanceLifecycleStatus lifecycleStatus)
    {
        LifecycleStatus = lifecycleStatus;
    }

    public void RecordHealth(InstanceHealthStatus healthStatus, DateTimeOffset observedAtUtc)
    {
        HealthStatus = healthStatus;
        LastHealthCheckAtUtc = observedAtUtc;

        if (healthStatus is not InstanceHealthStatus.Offline and not InstanceHealthStatus.Unknown)
        {
            LastSeenAtUtc = observedAtUtc;
        }
    }
}
