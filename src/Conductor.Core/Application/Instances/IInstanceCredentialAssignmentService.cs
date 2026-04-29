using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Application.Instances;

public interface IInstanceCredentialAssignmentService
{
    Task<InstanceCredentialAssignmentResult> AssignAsync(
        InstanceCredentialAssignmentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record InstanceCredentialAssignmentRequest(
    SymphonyInstanceId InstanceId,
    CredentialAssignmentSelection GitHubCredential,
    CredentialAssignmentSelection OpenAiCredential,
    string? RequestedByUserId = null);

public sealed record CredentialAssignmentSelection(
    CredentialInheritanceMode InheritanceMode,
    SecretId? SecretId = null);

public sealed record InstanceCredentialAssignmentResult(
    string InstanceId,
    CredentialAssignmentSummary GitHubCredential,
    CredentialAssignmentSummary OpenAiCredential,
    DateTimeOffset AssignedAtUtc);

public sealed record CredentialAssignmentSummary(
    string InheritanceMode,
    string? SecretId,
    string? SecretName,
    string? SecretType,
    string? ScopeType,
    string? ScopeId);

public sealed class InstanceCredentialAssignmentValidationException : Exception
{
    public InstanceCredentialAssignmentValidationException(
        IReadOnlyDictionary<string, string[]> errors)
        : base("Instance credential assignment failed validation.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class SymphonyInstanceNotFoundException : Exception
{
    public SymphonyInstanceNotFoundException(string instanceId)
        : base($"Symphony instance {instanceId} was not found.")
    {
        InstanceId = instanceId;
    }

    public string InstanceId { get; }
}
