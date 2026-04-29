using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Auditing;

public sealed class AuditEvent
{
    public AuditEvent(
        AuditEventId id,
        string actorUserId,
        string action,
        string targetResourceType,
        string? targetResourceId,
        DateTimeOffset occurredAtUtc,
        AuditEventOutcome outcome,
        string? correlationId,
        string? message,
        string? metadataJson)
    {
        Id = id;
        ActorUserId = Guard.NotWhiteSpace(actorUserId, nameof(actorUserId));
        Action = Guard.NotWhiteSpace(action, nameof(action));
        TargetResourceType = Guard.NotWhiteSpace(targetResourceType, nameof(targetResourceType));
        TargetResourceId = Guard.OptionalTrimmed(targetResourceId);
        OccurredAtUtc = occurredAtUtc;
        Outcome = outcome;
        CorrelationId = Guard.OptionalTrimmed(correlationId);
        Message = Guard.OptionalTrimmed(message);
        MetadataJson = Guard.OptionalTrimmed(metadataJson);
    }

    public AuditEventId Id { get; }

    public string ActorUserId { get; }

    public string Action { get; }

    public string TargetResourceType { get; }

    public string? TargetResourceId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public AuditEventOutcome Outcome { get; }

    public string? CorrelationId { get; }

    public string? Message { get; }

    public string? MetadataJson { get; }
}
