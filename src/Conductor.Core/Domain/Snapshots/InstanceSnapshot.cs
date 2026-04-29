using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Snapshots;

public sealed class InstanceSnapshot
{
    public InstanceSnapshot(
        InstanceSnapshotId id,
        SymphonyInstanceId symphonyInstanceId,
        DateTimeOffset capturedAtUtc,
        InstanceHealthStatus healthStatus,
        string? healthJson,
        string? runtimeJson,
        string? stateJson,
        int activeIssueCount,
        int runningSessionCount,
        int retryQueueCount,
        int failedRunCount,
        long tokenInputTotal,
        long tokenOutputTotal)
    {
        Id = id;
        SymphonyInstanceId = symphonyInstanceId;
        CapturedAtUtc = capturedAtUtc;
        HealthStatus = healthStatus;
        HealthJson = Guard.OptionalTrimmed(healthJson);
        RuntimeJson = Guard.OptionalTrimmed(runtimeJson);
        StateJson = Guard.OptionalTrimmed(stateJson);
        ActiveIssueCount = Guard.NonNegative(activeIssueCount, nameof(activeIssueCount));
        RunningSessionCount = Guard.NonNegative(runningSessionCount, nameof(runningSessionCount));
        RetryQueueCount = Guard.NonNegative(retryQueueCount, nameof(retryQueueCount));
        FailedRunCount = Guard.NonNegative(failedRunCount, nameof(failedRunCount));
        TokenInputTotal = Guard.NonNegative(tokenInputTotal, nameof(tokenInputTotal));
        TokenOutputTotal = Guard.NonNegative(tokenOutputTotal, nameof(tokenOutputTotal));
    }

    public InstanceSnapshotId Id { get; }

    public SymphonyInstanceId SymphonyInstanceId { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public InstanceHealthStatus HealthStatus { get; }

    public string? HealthJson { get; }

    public string? RuntimeJson { get; }

    public string? StateJson { get; }

    public int ActiveIssueCount { get; }

    public int RunningSessionCount { get; }

    public int RetryQueueCount { get; }

    public int FailedRunCount { get; }

    public long TokenInputTotal { get; }

    public long TokenOutputTotal { get; }

    public long TokenTotal => TokenInputTotal + TokenOutputTotal;
}
