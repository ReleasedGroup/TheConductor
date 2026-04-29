using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Runs;

public sealed class RunAttempt
{
    public RunAttempt(
        RunAttemptId id,
        RunId runId,
        int attemptNumber,
        RunStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? finishedAtUtc,
        string? exitReason,
        string? errorDetail)
    {
        if (finishedAtUtc.HasValue && finishedAtUtc.Value < startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(finishedAtUtc), "Finished time cannot be before the attempt started.");
        }

        Id = id;
        RunId = runId;
        AttemptNumber = Guard.Positive(attemptNumber, nameof(attemptNumber));
        Status = status;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        ExitReason = Guard.OptionalTrimmed(exitReason);
        ErrorDetail = Guard.OptionalTrimmed(errorDetail);
    }

    public RunAttemptId Id { get; }

    public RunId RunId { get; }

    public int AttemptNumber { get; }

    public RunStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public string? ExitReason { get; private set; }

    public string? ErrorDetail { get; private set; }

    public void Complete(RunStatus terminalStatus, DateTimeOffset finishedAtUtc, string? exitReason, string? errorDetail)
    {
        if (terminalStatus is RunStatus.Queued or RunStatus.Running)
        {
            throw new ArgumentException("A terminal run status is required.", nameof(terminalStatus));
        }

        if (finishedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(finishedAtUtc), "Finished time cannot be before the attempt started.");
        }

        Status = terminalStatus;
        FinishedAtUtc = finishedAtUtc;
        ExitReason = Guard.OptionalTrimmed(exitReason);
        ErrorDetail = Guard.OptionalTrimmed(errorDetail);
    }
}
