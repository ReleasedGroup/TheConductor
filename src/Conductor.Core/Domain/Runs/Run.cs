using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Runs;

public sealed class Run
{
    public Run(
        RunId id,
        SymphonyInstanceId symphonyInstanceId,
        RepositoryId repositoryId,
        int gitHubIssueNumber,
        string? symphonyRunId,
        RunStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? finishedAtUtc,
        int attemptCount,
        long tokenInput,
        long tokenOutput,
        string? errorSummary,
        string? branchName,
        Uri? pullRequestUrl)
    {
        if (finishedAtUtc.HasValue && finishedAtUtc.Value < startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(finishedAtUtc), "Finished time cannot be before the run started.");
        }

        Id = id;
        SymphonyInstanceId = symphonyInstanceId;
        RepositoryId = repositoryId;
        GitHubIssueNumber = Guard.Positive(gitHubIssueNumber, nameof(gitHubIssueNumber));
        SymphonyRunId = Guard.OptionalTrimmed(symphonyRunId);
        Status = status;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        AttemptCount = Guard.NonNegative(attemptCount, nameof(attemptCount));
        TokenInput = Guard.NonNegative(tokenInput, nameof(tokenInput));
        TokenOutput = Guard.NonNegative(tokenOutput, nameof(tokenOutput));
        ErrorSummary = Guard.OptionalTrimmed(errorSummary);
        BranchName = Guard.OptionalTrimmed(branchName);
        PullRequestUrl = Guard.OptionalAbsoluteUri(pullRequestUrl, nameof(pullRequestUrl));
    }

    public RunId Id { get; }

    public SymphonyInstanceId SymphonyInstanceId { get; }

    public RepositoryId RepositoryId { get; }

    public int GitHubIssueNumber { get; }

    public string? SymphonyRunId { get; }

    public RunStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public long TokenInput { get; private set; }

    public long TokenOutput { get; private set; }

    public long TokenTotal => TokenInput + TokenOutput;

    public string? ErrorSummary { get; private set; }

    public string? BranchName { get; }

    public Uri? PullRequestUrl { get; }

    public void RecordAttempt()
    {
        AttemptCount++;
    }

    public void RecordTokenUsage(long tokenInput, long tokenOutput)
    {
        TokenInput = Guard.NonNegative(tokenInput, nameof(tokenInput));
        TokenOutput = Guard.NonNegative(tokenOutput, nameof(tokenOutput));
    }

    public void Complete(RunStatus terminalStatus, DateTimeOffset finishedAtUtc, string? errorSummary)
    {
        if (terminalStatus is RunStatus.Queued or RunStatus.Running)
        {
            throw new ArgumentException("A terminal run status is required.", nameof(terminalStatus));
        }

        if (finishedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(finishedAtUtc), "Finished time cannot be before the run started.");
        }

        Status = terminalStatus;
        FinishedAtUtc = finishedAtUtc;
        ErrorSummary = Guard.OptionalTrimmed(errorSummary);
    }
}
