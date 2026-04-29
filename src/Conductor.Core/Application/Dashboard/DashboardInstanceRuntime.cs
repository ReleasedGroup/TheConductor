using Conductor.Core.Domain;

namespace Conductor.Core.Application.Dashboard;

public sealed class DashboardInstanceRuntime
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string RepositoryFullName { get; init; } = string.Empty;

    public Uri? BaseUrl { get; init; }

    public InstanceHealthStatus HealthStatus { get; init; } = InstanceHealthStatus.Unknown;

    public InstanceLifecycleStatus LifecycleStatus { get; init; } = InstanceLifecycleStatus.NotProvisioned;

    public string? SymphonyVersion { get; init; }

    public string? WorkflowOwner { get; init; }

    public string? WorkflowRepository { get; init; }

    public string? WorkflowSourcePath { get; init; }

    public DateTimeOffset? LastHealthCheckAtUtc { get; init; }

    public DateTimeOffset? LastSnapshotCapturedAtUtc { get; init; }

    public DateTimeOffset? LastSeenAtUtc { get; init; }

    public int ActiveIssueCount { get; init; }

    public int RunningSessionCount { get; init; }

    public int RetryQueueCount { get; init; }

    public int FailedRunCount { get; init; }

    public long TokenTotal { get; init; }
}
