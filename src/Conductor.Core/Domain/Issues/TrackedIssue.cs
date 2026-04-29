using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Issues;

public sealed class TrackedIssue
{
    public TrackedIssue(
        TrackedIssueId id,
        RepositoryId repositoryId,
        int gitHubIssueNumber,
        string title,
        TrackedIssueState state,
        string? labelsJson,
        string? milestone,
        string? assigneeLoginsJson,
        Uri url,
        SymphonyIssueStatus symphonyStatus,
        RunStatus? lastRunStatus,
        DateTimeOffset lastActivityAtUtc,
        bool isBlocked,
        string? blockerReason)
    {
        Id = id;
        RepositoryId = repositoryId;
        GitHubIssueNumber = Guard.Positive(gitHubIssueNumber, nameof(gitHubIssueNumber));
        Title = Guard.NotWhiteSpace(title, nameof(title));
        State = state;
        LabelsJson = Guard.OptionalTrimmed(labelsJson);
        Milestone = Guard.OptionalTrimmed(milestone);
        AssigneeLoginsJson = Guard.OptionalTrimmed(assigneeLoginsJson);
        Url = Guard.AbsoluteUri(url, nameof(url));
        SymphonyStatus = symphonyStatus;
        LastRunStatus = lastRunStatus;
        LastActivityAtUtc = lastActivityAtUtc;
        IsBlocked = isBlocked;
        BlockerReason = isBlocked ? Guard.OptionalTrimmed(blockerReason) : null;
    }

    public TrackedIssueId Id { get; }

    public RepositoryId RepositoryId { get; }

    public int GitHubIssueNumber { get; }

    public string Title { get; }

    public TrackedIssueState State { get; }

    public string? LabelsJson { get; }

    public string? Milestone { get; }

    public string? AssigneeLoginsJson { get; }

    public Uri Url { get; }

    public SymphonyIssueStatus SymphonyStatus { get; private set; }

    public RunStatus? LastRunStatus { get; private set; }

    public DateTimeOffset LastActivityAtUtc { get; private set; }

    public bool IsBlocked { get; private set; }

    public string? BlockerReason { get; private set; }

    public void MarkBlocked(string blockerReason, DateTimeOffset blockedAtUtc)
    {
        IsBlocked = true;
        BlockerReason = Guard.NotWhiteSpace(blockerReason, nameof(blockerReason));
        LastActivityAtUtc = blockedAtUtc;
    }

    public void ClearBlocker(DateTimeOffset clearedAtUtc)
    {
        IsBlocked = false;
        BlockerReason = null;
        LastActivityAtUtc = clearedAtUtc;
    }

    public void UpdateSymphonyStatus(
        SymphonyIssueStatus symphonyStatus,
        RunStatus? lastRunStatus,
        DateTimeOffset activityAtUtc)
    {
        SymphonyStatus = symphonyStatus;
        LastRunStatus = lastRunStatus;
        LastActivityAtUtc = activityAtUtc;
    }
}
