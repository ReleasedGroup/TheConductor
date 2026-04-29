namespace Conductor.Infrastructure.Persistence.Sqlite.Schema;

internal sealed class ProjectRecord
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class RepositoryRecord
{
    public string Id { get; set; } = string.Empty;

    public string? ProjectId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = string.Empty;

    public string CloneUrl { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public int OpenIssueCount { get; set; }

    public int PullRequestCount { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class WorkflowProfileRecord
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ExecutionMode { get; set; }

    public string WorkflowSource { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class SymphonyReleaseArtifactRecord
{
    public string Id { get; set; } = string.Empty;

    public string ReleaseTag { get; set; } = string.Empty;

    public string AssetName { get; set; } = string.Empty;

    public string? TargetRuntime { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string? LocalPath { get; set; }

    public string? Checksum { get; set; }

    public DateTimeOffset DownloadedAtUtc { get; set; }

    public string? MetadataJson { get; set; }
}

internal sealed class SymphonyInstanceRecord
{
    public string Id { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string? WorkflowProfileId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutionMode { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string HealthStatus { get; set; } = string.Empty;

    public string DeliveryStatus { get; set; } = string.Empty;

    public string? ReleaseSelector { get; set; }

    public string? ResolvedReleaseTag { get; set; }

    public string? GitHubSecretId { get; set; }

    public string? OpenAiSecretId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastHealthCheckAtUtc { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }
}

internal sealed class InstanceSnapshotRecord
{
    public string Id { get; set; } = string.Empty;

    public string SymphonyInstanceId { get; set; } = string.Empty;

    public DateTimeOffset CapturedAtUtc { get; set; }

    public string HealthStatus { get; set; } = string.Empty;

    public int? HttpStatusCode { get; set; }

    public long? LatencyMilliseconds { get; set; }

    public string? ErrorMessage { get; set; }

    public string? HealthJson { get; set; }

    public string? RuntimeJson { get; set; }

    public string? StateJson { get; set; }
}

internal sealed class TrackedIssueRecord
{
    public string Id { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public long GitHubIssueNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SymphonyStatus { get; set; } = string.Empty;

    public bool IsBlocked { get; set; }

    public string? Url { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? LabelsJson { get; set; }

    public string? AssigneesJson { get; set; }

    public string? PullRequestsJson { get; set; }
}

internal sealed class RunRecord
{
    public string Id { get; set; } = string.Empty;

    public string SymphonyInstanceId { get; set; } = string.Empty;

    public string RepositoryId { get; set; } = string.Empty;

    public string? TrackedIssueId { get; set; }

    public long? GitHubIssueNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? BranchName { get; set; }

    public string? PullRequestUrl { get; set; }

    public string? Summary { get; set; }
}

internal sealed class RunAttemptRecord
{
    public string Id { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public string? LogPath { get; set; }
}

internal sealed class EventRecord
{
    public string Id { get; set; } = string.Empty;

    public string? SymphonyInstanceId { get; set; }

    public string? RepositoryId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? PayloadJson { get; set; }
}

internal sealed class AlertRecord
{
    public string Id { get; set; } = string.Empty;

    public string? SymphonyInstanceId { get; set; }

    public string? RepositoryId { get; set; }

    public string? TrackedIssueId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public string? MetadataJson { get; set; }
}

internal sealed class ReportRecord
{
    public string Id { get; set; } = string.Empty;

    public string? ProjectId { get; set; }

    public string? RepositoryId { get; set; }

    public string ReportType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string? GeneratedByUserId { get; set; }

    public string? ContentMarkdown { get; set; }

    public string? ContentHtml { get; set; }

    public string? MetadataJson { get; set; }
}

internal sealed class SecretDescriptorRecord
{
    public string Id { get; set; } = string.Empty;

    public string ScopeType { get; set; } = string.Empty;

    public string? ScopeId { get; set; }

    public string SecretType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RotatedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }
}

internal sealed class AuditEventRecord
{
    public string Id { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? MetadataJson { get; set; }
}

internal sealed class BackgroundOperationRecord
{
    public string Id { get; set; } = string.Empty;

    public string? SymphonyInstanceId { get; set; }

    public string? RepositoryId { get; set; }

    public string OperationType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? RequestedByUserId { get; set; }

    public string? ErrorMessage { get; set; }

    public string? PayloadJson { get; set; }
}
