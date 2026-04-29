using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Operations;

public sealed class BackgroundOperation
{
    public BackgroundOperation(
        BackgroundOperationId id,
        string operationType,
        DateTimeOffset createdAtUtc,
        string? targetResourceType,
        string? targetResourceId,
        string? correlationId)
    {
        Id = id;
        OperationType = Guard.NotWhiteSpace(operationType, nameof(operationType));
        Status = BackgroundOperationStatus.Queued;
        CreatedAtUtc = createdAtUtc;
        TargetResourceType = Guard.OptionalTrimmed(targetResourceType);
        TargetResourceId = Guard.OptionalTrimmed(targetResourceId);
        CorrelationId = Guard.OptionalTrimmed(correlationId);
    }

    public BackgroundOperationId Id { get; }

    public string OperationType { get; }

    public BackgroundOperationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? TargetResourceType { get; }

    public string? TargetResourceId { get; }

    public string? CorrelationId { get; }

    public string? Summary { get; private set; }

    public string? ErrorDetail { get; private set; }

    public void MarkRunning(DateTimeOffset startedAtUtc)
    {
        EnsureNotBeforeCreated(startedAtUtc, nameof(startedAtUtc));

        Status = BackgroundOperationStatus.Running;
        StartedAtUtc = startedAtUtc;
    }

    public void Succeed(DateTimeOffset completedAtUtc, string? summary)
    {
        Complete(BackgroundOperationStatus.Succeeded, completedAtUtc, summary, errorDetail: null);
    }

    public void Fail(DateTimeOffset completedAtUtc, string errorDetail)
    {
        Complete(
            BackgroundOperationStatus.Failed,
            completedAtUtc,
            summary: null,
            Guard.NotWhiteSpace(errorDetail, nameof(errorDetail)));
    }

    public void Cancel(DateTimeOffset completedAtUtc, string? summary)
    {
        Complete(BackgroundOperationStatus.Canceled, completedAtUtc, summary, errorDetail: null);
    }

    private void Complete(
        BackgroundOperationStatus status,
        DateTimeOffset completedAtUtc,
        string? summary,
        string? errorDetail)
    {
        EnsureNotBeforeCreated(completedAtUtc, nameof(completedAtUtc));

        if (StartedAtUtc.HasValue && completedAtUtc < StartedAtUtc.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAtUtc), "Completed time cannot be before the operation started.");
        }

        Status = status;
        CompletedAtUtc = completedAtUtc;
        Summary = Guard.OptionalTrimmed(summary);
        ErrorDetail = errorDetail;
    }

    private void EnsureNotBeforeCreated(DateTimeOffset value, string parameterName)
    {
        if (value < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Timestamp cannot be before the operation was created.");
        }
    }
}
