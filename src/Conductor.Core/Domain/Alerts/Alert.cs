using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Alerts;

public sealed class Alert
{
    public Alert(
        AlertId id,
        AlertSeverity severity,
        string source,
        string summary,
        string? recommendedAction,
        DateTimeOffset createdAtUtc,
        SymphonyInstanceId? symphonyInstanceId = null,
        RepositoryId? repositoryId = null,
        RunId? runId = null,
        int? gitHubIssueNumber = null)
    {
        Id = id;
        Status = AlertStatus.Active;
        Severity = severity;
        Source = Guard.NotWhiteSpace(source, nameof(source));
        Summary = Guard.NotWhiteSpace(summary, nameof(summary));
        RecommendedAction = Guard.OptionalTrimmed(recommendedAction);
        CreatedAtUtc = createdAtUtc;
        SymphonyInstanceId = symphonyInstanceId;
        RepositoryId = repositoryId;
        RunId = runId;
        GitHubIssueNumber = gitHubIssueNumber.HasValue
            ? Guard.Positive(gitHubIssueNumber.Value, nameof(gitHubIssueNumber))
            : null;
    }

    public AlertId Id { get; }

    public AlertStatus Status { get; private set; }

    public AlertSeverity Severity { get; }

    public string Source { get; }

    public string Summary { get; }

    public string? RecommendedAction { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public string? ResolutionNote { get; private set; }

    public SymphonyInstanceId? SymphonyInstanceId { get; }

    public RepositoryId? RepositoryId { get; }

    public RunId? RunId { get; }

    public int? GitHubIssueNumber { get; }

    public void Acknowledge()
    {
        if (Status == AlertStatus.Active)
        {
            Status = AlertStatus.Acknowledged;
        }
    }

    public void Resolve(DateTimeOffset resolvedAtUtc, string? resolutionNote)
    {
        if (resolvedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(resolvedAtUtc), "Resolved time cannot be before the alert was created.");
        }

        Status = AlertStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
        ResolutionNote = Guard.OptionalTrimmed(resolutionNote);
    }
}
