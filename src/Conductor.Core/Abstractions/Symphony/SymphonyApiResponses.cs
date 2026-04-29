using Conductor.Core.Domain;

namespace Conductor.Core.Abstractions.Symphony;

public sealed record SymphonyHealthResponse(
    InstanceHealthStatus Status,
    int? HttpStatusCode,
    TimeSpan Latency,
    string RawJson)
{
    public string? RawStatus { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record SymphonyRuntimeResponse(string RawJson)
{
    public string? ApplicationName { get; init; }

    public string? ApplicationVersion { get; init; }

    public string? InstanceId { get; init; }

    public string? LeaseName { get; init; }

    public int? LeaseTtlSeconds { get; init; }

    public string? PersistenceProvider { get; init; }

    public bool? PersistenceConfigured { get; init; }

    public string? WorkflowSourcePath { get; init; }

    public string? WorkflowOwner { get; init; }

    public string? WorkflowRepository { get; init; }

    public string? WorkflowMilestone { get; init; }

    public int? PollingIntervalMs { get; init; }

    public int? MaxConcurrentAgents { get; init; }

    public int? MaxTurns { get; init; }

    public string? WorkspaceRoot { get; init; }

    public string? WorkspaceBaseBranch { get; init; }

    public string? WorkflowError { get; init; }
}

public sealed record SymphonyWorkflowDocument(string Source, string? ETag);

public sealed record SymphonyStateResponse(string RawJson)
{
    public int RunningCount { get; init; }

    public int RetryingCount { get; init; }

    public int TrackedIssueCount { get; init; }

    public IReadOnlyList<SymphonyRunningSession> RunningSessions { get; init; } =
        Array.Empty<SymphonyRunningSession>();

    public IReadOnlyList<SymphonyRetryQueueEntry> RetryQueue { get; init; } =
        Array.Empty<SymphonyRetryQueueEntry>();

    public IReadOnlyList<SymphonyTrackedIssueStateCount> TrackedIssueDistribution { get; init; } =
        Array.Empty<SymphonyTrackedIssueStateCount>();

    public IReadOnlyList<SymphonyRecentActivity> RecentActivity { get; init; } =
        Array.Empty<SymphonyRecentActivity>();

    public IReadOnlyList<SymphonyLeaseState> Leases { get; init; } =
        Array.Empty<SymphonyLeaseState>();

    public SymphonyTokenTotals TokenTotals { get; init; } = SymphonyTokenTotals.Empty;

    public bool HasRateLimits { get; init; }
}

public sealed record SymphonyIssueResponse(string IssueIdentifier, string RawJson)
{
    public string? IssueId { get; init; }

    public string? Status { get; init; }

    public string? WorkspacePath { get; init; }

    public int RestartCount { get; init; }

    public int? CurrentRetryAttempt { get; init; }

    public SymphonyRunningSession? Running { get; init; }

    public SymphonyRetryQueueEntry? Retry { get; init; }

    public IReadOnlyList<SymphonyRecentActivity> RecentEvents { get; init; } =
        Array.Empty<SymphonyRecentActivity>();

    public string? LastError { get; init; }

    public string? Title { get; init; }

    public Uri? Url { get; init; }

    public int? Priority { get; init; }

    public string? CacheState { get; init; }

    public string? Milestone { get; init; }

    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();

    public IReadOnlyList<SymphonyBlockerReference> BlockedBy { get; init; } =
        Array.Empty<SymphonyBlockerReference>();

    public IReadOnlyList<SymphonyPullRequestReference> PullRequests { get; init; } =
        Array.Empty<SymphonyPullRequestReference>();
}

public sealed record SymphonyRefreshResponse(bool Accepted, string RawJson);

public sealed record SymphonyTokenTotals(long InputTokens, long OutputTokens, long TotalTokens)
{
    public static SymphonyTokenTotals Empty { get; } = new(0, 0, 0);
}

public sealed record SymphonyRunningSession(
    string? IssueId,
    string? IssueIdentifier,
    string? Title,
    Uri? Url,
    string? Milestone,
    IReadOnlyList<string> Labels,
    string? State,
    string? SessionId,
    int TurnCount,
    string? LastEvent,
    string? LastMessage,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastEventAtUtc,
    SymphonyTokenTotals Tokens);

public sealed record SymphonyRetryQueueEntry(
    string? IssueId,
    string? IssueIdentifier,
    string? Title,
    Uri? Url,
    string? Milestone,
    IReadOnlyList<string> Labels,
    int Attempt,
    DateTimeOffset? DueAtUtc,
    string? Error);

public sealed record SymphonyTrackedIssueStateCount(string State, int Count);

public sealed record SymphonyRecentActivity(
    DateTimeOffset? AtUtc,
    string? IssueId,
    string? IssueIdentifier,
    string? SessionId,
    string? Level,
    string? Event,
    string? Message);

public sealed record SymphonyLeaseState(
    string? LeaseName,
    string? OwnerInstanceId,
    DateTimeOffset? AcquiredAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired);

public sealed record SymphonyBlockerReference(string? Id, string? Identifier, string? State);

public sealed record SymphonyPullRequestReference(
    string? Id,
    int? Number,
    string? State,
    Uri? Url,
    string? HeadRef,
    string? BaseRef);
