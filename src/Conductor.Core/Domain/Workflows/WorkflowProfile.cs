using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Workflows;

public sealed class WorkflowProfile
{
    public WorkflowProfile(
        WorkflowProfileId id,
        string name,
        string workflowSource,
        DateTimeOffset createdAtUtc)
        : this(
            id,
            name,
            description: null,
            workflowSource,
            isDefault: false,
            createdAtUtc,
            updatedAtUtc: createdAtUtc,
            revision: 1)
    {
    }

    public WorkflowProfile(
        WorkflowProfileId id,
        string name,
        string? description,
        string workflowSource,
        bool isDefault,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        int revision = 1)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), revision, "Workflow profile revision must be at least 1.");
        }

        Id = new WorkflowProfileId(Guard.NotEmpty(id.Value, nameof(id)));
        Name = Guard.NotWhiteSpace(name, nameof(name));
        Description = Guard.OptionalTrimmed(description);
        WorkflowSource = Guard.NotWhiteSpace(workflowSource, nameof(workflowSource));
        IsDefault = isDefault;
        CreatedAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        Revision = revision;

        if (UpdatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.", nameof(updatedAtUtc));
        }
    }

    public WorkflowProfileId Id { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string WorkflowSource { get; private set; }

    public bool IsDefault { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string? description,
        string workflowSource,
        bool isDefault,
        DateTimeOffset updatedAtUtc)
    {
        var validatedName = Guard.NotWhiteSpace(name, nameof(name));
        var validatedDescription = Guard.OptionalTrimmed(description);
        var validatedWorkflowSource = Guard.NotWhiteSpace(workflowSource, nameof(workflowSource));
        var validatedUpdatedAtUtc = ValidateUpdatedAt(updatedAtUtc);

        if (Name == validatedName &&
            Description == validatedDescription &&
            WorkflowSource == validatedWorkflowSource &&
            IsDefault == isDefault)
        {
            return;
        }

        Name = validatedName;
        Description = validatedDescription;
        WorkflowSource = validatedWorkflowSource;
        IsDefault = isDefault;
        RecordRevision(validatedUpdatedAtUtc);
    }

    public void MarkDefault(DateTimeOffset updatedAtUtc)
    {
        if (IsDefault)
        {
            return;
        }

        IsDefault = true;
        RecordRevision(ValidateUpdatedAt(updatedAtUtc));
    }

    public void ClearDefault(DateTimeOffset updatedAtUtc)
    {
        if (!IsDefault)
        {
            return;
        }

        IsDefault = false;
        RecordRevision(ValidateUpdatedAt(updatedAtUtc));
    }

    private void RecordRevision(DateTimeOffset updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
        Revision++;
    }

    private DateTimeOffset ValidateUpdatedAt(DateTimeOffset updatedAtUtc)
    {
        var utcUpdatedAt = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));

        if (utcUpdatedAt < CreatedAtUtc)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.", nameof(updatedAtUtc));
        }

        if (utcUpdatedAt < UpdatedAtUtc)
        {
            throw new ArgumentException("Updated timestamp cannot move backwards.", nameof(updatedAtUtc));
        }

        return utcUpdatedAt;
    }
}
