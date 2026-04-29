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
        long tokenOutputTotal,
        int? httpStatusCode = null,
        long? latencyMilliseconds = null,
        string? errorMessage = null,
        string? applicationName = null,
        string? applicationVersion = null,
        string? runtimeInstanceId = null,
        string? workflowOwner = null,
        string? workflowRepository = null,
        string? workflowSourcePath = null,
        string? persistenceProvider = null,
        string? runtimeDefaultsJson = null)
    {
        Id = id;
        SymphonyInstanceId = symphonyInstanceId;
        CapturedAtUtc = capturedAtUtc;
        HealthStatus = healthStatus;
        HttpStatusCode = NonNegativeOrNull(httpStatusCode, nameof(httpStatusCode));
        LatencyMilliseconds = NonNegativeOrNull(latencyMilliseconds, nameof(latencyMilliseconds));
        ErrorMessage = Guard.OptionalTrimmed(errorMessage);
        HealthJson = Guard.OptionalTrimmed(healthJson);
        RuntimeJson = Guard.OptionalTrimmed(runtimeJson);
        StateJson = Guard.OptionalTrimmed(stateJson);
        ApplicationName = Guard.OptionalTrimmed(applicationName);
        ApplicationVersion = Guard.OptionalTrimmed(applicationVersion);
        RuntimeInstanceId = Guard.OptionalTrimmed(runtimeInstanceId);
        WorkflowOwner = Guard.OptionalTrimmed(workflowOwner);
        WorkflowRepository = Guard.OptionalTrimmed(workflowRepository);
        WorkflowSourcePath = Guard.OptionalTrimmed(workflowSourcePath);
        PersistenceProvider = Guard.OptionalTrimmed(persistenceProvider);
        RuntimeDefaultsJson = Guard.OptionalTrimmed(runtimeDefaultsJson);
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

    public int? HttpStatusCode { get; }

    public long? LatencyMilliseconds { get; }

    public string? ErrorMessage { get; }

    public string? HealthJson { get; }

    public string? RuntimeJson { get; }

    public string? StateJson { get; }

    public string? ApplicationName { get; }

    public string? ApplicationVersion { get; }

    public string? RuntimeInstanceId { get; }

    public string? WorkflowOwner { get; }

    public string? WorkflowRepository { get; }

    public string? WorkflowSourcePath { get; }

    public string? PersistenceProvider { get; }

    public string? RuntimeDefaultsJson { get; }

    public int ActiveIssueCount { get; }

    public int RunningSessionCount { get; }

    public int RetryQueueCount { get; }

    public int FailedRunCount { get; }

    public long TokenInputTotal { get; }

    public long TokenOutputTotal { get; }

    public long TokenTotal => TokenInputTotal + TokenOutputTotal;

    private static int? NonNegativeOrNull(int? value, string parameterName) =>
        value is null ? null : Guard.NonNegative(value.Value, parameterName);

    private static long? NonNegativeOrNull(long? value, string parameterName) =>
        value is null ? null : Guard.NonNegative(value.Value, parameterName);
}
