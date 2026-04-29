using Conductor.Core.Common;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Workflows;

namespace Conductor.Core.Application.Workflows;

public interface IWorkflowValidationService
{
    WorkflowValidationResult Validate(WorkflowValidationRequest request);
}

public sealed record WorkflowValidationRequest(
    GitHubRepositoryFullName Repository,
    WorkflowProfile WorkflowProfile,
    ExecutionMode ExecutionMode,
    int? Port,
    string WorkflowPath,
    string DataPath,
    IReadOnlyCollection<WorkflowSecretReference> SecretReferences);

public sealed record WorkflowSecretReference
{
    public WorkflowSecretReference(
        SecretType secretType,
        CredentialInheritanceMode inheritanceMode,
        SecretId? secretId = null,
        bool isResolved = false)
    {
        if (!Enum.IsDefined(inheritanceMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(inheritanceMode),
                inheritanceMode,
                "Credential inheritance mode is not supported.");
        }

        if (inheritanceMode == CredentialInheritanceMode.SpecificSecret)
        {
            if (secretId is null)
            {
                throw new ArgumentException("A specific workflow secret reference requires a secret id.", nameof(secretId));
            }

            SecretId = new SecretId(Guard.NotEmpty(secretId.Value.Value, nameof(secretId)));
        }
        else if (secretId is not null)
        {
            throw new ArgumentException("Secret ids can only be set for specific workflow secret references.", nameof(secretId));
        }

        SecretType = secretType;
        InheritanceMode = inheritanceMode;
        IsResolved = isResolved || inheritanceMode == CredentialInheritanceMode.SpecificSecret;
    }

    public SecretType SecretType { get; }

    public CredentialInheritanceMode InheritanceMode { get; }

    public SecretId? SecretId { get; }

    public bool IsResolved { get; }

    public string EnvironmentVariableName => SecretTypeMetadata.Get(SecretType).EnvironmentVariableName;
}

public sealed record WorkflowValidationResult(IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static WorkflowValidationResult Success { get; } =
        new(new Dictionary<string, string[]>(StringComparer.Ordinal));
}
