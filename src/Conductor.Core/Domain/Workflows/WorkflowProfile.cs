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
    {
        Id = id;
        Name = Guard.NotWhiteSpace(name, nameof(name));
        WorkflowSource = Guard.NotWhiteSpace(workflowSource, nameof(workflowSource));
        CreatedAtUtc = createdAtUtc;
    }

    public WorkflowProfileId Id { get; }

    public string Name { get; }

    public string WorkflowSource { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
