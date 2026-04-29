using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Events;

public sealed class Event
{
    public Event(
        EventId id,
        SymphonyInstanceId? symphonyInstanceId,
        RepositoryId? repositoryId,
        int? issueNumber,
        EventSeverity severity,
        string eventType,
        string message,
        string? payloadJson,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        SymphonyInstanceId = symphonyInstanceId;
        RepositoryId = repositoryId;
        IssueNumber = issueNumber.HasValue ? Guard.Positive(issueNumber.Value, nameof(issueNumber)) : null;
        Severity = severity;
        EventType = Guard.NotWhiteSpace(eventType, nameof(eventType));
        Message = Guard.NotWhiteSpace(message, nameof(message));
        PayloadJson = Guard.OptionalTrimmed(payloadJson);
        OccurredAtUtc = occurredAtUtc;
    }

    public EventId Id { get; }

    public SymphonyInstanceId? SymphonyInstanceId { get; }

    public RepositoryId? RepositoryId { get; }

    public int? IssueNumber { get; }

    public EventSeverity Severity { get; }

    public string EventType { get; }

    public string Message { get; }

    public string? PayloadJson { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}
