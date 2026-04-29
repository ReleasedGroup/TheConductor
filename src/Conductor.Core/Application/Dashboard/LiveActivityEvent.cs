using Conductor.Core.Common;
using Conductor.Core.Domain;

namespace Conductor.Core.Application.Dashboard;

public sealed record LiveActivityEvent
{
    public LiveActivityEvent(
        DateTimeOffset occurredAtUtc,
        EventSeverity severity,
        string eventType,
        string sourceName,
        int? issueNumber,
        string message)
    {
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "A known event severity is required.");
        }

        OccurredAtUtc = occurredAtUtc;
        Severity = severity;
        EventType = Guard.NotWhiteSpace(eventType, nameof(eventType));
        SourceName = Guard.NotWhiteSpace(sourceName, nameof(sourceName));
        IssueNumber = issueNumber.HasValue ? Guard.Positive(issueNumber.Value, nameof(issueNumber)) : null;
        Message = Guard.NotWhiteSpace(message, nameof(message));
    }

    public DateTimeOffset OccurredAtUtc { get; }

    public EventSeverity Severity { get; }

    public string EventType { get; }

    public string SourceName { get; }

    public int? IssueNumber { get; }

    public string Message { get; }
}
