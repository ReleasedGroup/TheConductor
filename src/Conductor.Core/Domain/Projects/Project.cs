using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Projects;

public sealed class Project
{
    public Project(
        ProjectId id,
        string name,
        string ownerName,
        string? description,
        string defaultBranchPolicy,
        ProjectStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = new ProjectId(Guard.NotEmpty(id.Value, nameof(id)));
        Name = Guard.NotWhiteSpace(name, nameof(name));
        OwnerName = Guard.NotWhiteSpace(ownerName, nameof(ownerName));
        Description = Guard.OptionalTrimmed(description);
        DefaultBranchPolicy = Guard.NotWhiteSpace(defaultBranchPolicy, nameof(defaultBranchPolicy));
        Status = status;
        CreatedAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));

        if (UpdatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.", nameof(updatedAtUtc));
        }
    }

    public ProjectId Id { get; }

    public string Name { get; private set; }

    public string OwnerName { get; private set; }

    public string? Description { get; private set; }

    public string DefaultBranchPolicy { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Rename(
        string name,
        string ownerName,
        DateTimeOffset updatedAtUtc)
    {
        var validatedName = Guard.NotWhiteSpace(name, nameof(name));
        var validatedOwnerName = Guard.NotWhiteSpace(ownerName, nameof(ownerName));
        var validatedUpdatedAtUtc = ValidateUpdatedAt(updatedAtUtc);

        Name = validatedName;
        OwnerName = validatedOwnerName;
        UpdatedAtUtc = validatedUpdatedAtUtc;
    }

    public void UpdateDetails(
        string? description,
        string defaultBranchPolicy,
        DateTimeOffset updatedAtUtc)
    {
        var validatedDescription = Guard.OptionalTrimmed(description);
        var validatedDefaultBranchPolicy = Guard.NotWhiteSpace(defaultBranchPolicy, nameof(defaultBranchPolicy));
        var validatedUpdatedAtUtc = ValidateUpdatedAt(updatedAtUtc);

        Description = validatedDescription;
        DefaultBranchPolicy = validatedDefaultBranchPolicy;
        UpdatedAtUtc = validatedUpdatedAtUtc;
    }

    public void Activate(DateTimeOffset activatedAtUtc)
    {
        var validatedActivatedAtUtc = ValidateUpdatedAt(activatedAtUtc);

        Status = ProjectStatus.Active;
        UpdatedAtUtc = validatedActivatedAtUtc;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        var validatedArchivedAtUtc = ValidateUpdatedAt(archivedAtUtc);

        Status = ProjectStatus.Archived;
        UpdatedAtUtc = validatedArchivedAtUtc;
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
