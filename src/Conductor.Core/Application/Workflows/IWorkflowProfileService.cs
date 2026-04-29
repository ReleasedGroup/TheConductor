using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.Workflows;

public interface IWorkflowProfileService
{
    Task<IReadOnlyList<WorkflowProfileSummary>> ListAsync(
        WorkflowProfileQuery query,
        CancellationToken cancellationToken = default);

    Task<WorkflowProfileDetail?> GetAsync(
        WorkflowProfileId profileId,
        CancellationToken cancellationToken = default);

    Task<WorkflowProfileMutationResult> CreateAsync(
        WorkflowProfileMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowProfileMutationResult> UpdateAsync(
        WorkflowProfileId profileId,
        WorkflowProfileMutationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowProfileQuery(
    string? Search = null);

public sealed record WorkflowProfileSummary(
    WorkflowProfileId Id,
    string Name,
    string? Description,
    bool IsDefault,
    int Revision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowProfileDetail(
    WorkflowProfileId Id,
    string Name,
    string? Description,
    string WorkflowSource,
    bool IsDefault,
    int Revision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowProfileMutationRequest(
    string Name,
    string? Description,
    string WorkflowSource,
    bool IsDefault = false,
    string? RequestedByUserId = null);

public sealed record WorkflowProfileMutationResult(
    WorkflowProfileId Id,
    string Name,
    bool IsDefault,
    int Revision,
    DateTimeOffset UpdatedAtUtc);

public sealed class WorkflowProfileValidationException : Exception
{
    public WorkflowProfileValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Workflow profile failed validation.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class WorkflowProfileNotFoundException : Exception
{
    public WorkflowProfileNotFoundException(WorkflowProfileId profileId)
        : base($"Workflow profile '{profileId}' was not found.")
    {
        ProfileId = profileId;
    }

    public WorkflowProfileId ProfileId { get; }
}
