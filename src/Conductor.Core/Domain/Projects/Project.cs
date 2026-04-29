using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Projects;

public sealed class Project
{
    public Project(
        ProjectId id,
        string name,
        string ownerName,
        ProjectStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Name = Guard.NotWhiteSpace(name, nameof(name));
        OwnerName = Guard.NotWhiteSpace(ownerName, nameof(ownerName));
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public ProjectId Id { get; }

    public string Name { get; }

    public string OwnerName { get; }

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        Status = ProjectStatus.Archived;
        UpdatedAtUtc = archivedAtUtc;
    }
}
