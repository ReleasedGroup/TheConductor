using Conductor.Core.Domain;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Application.Workflows;

public sealed class WorkflowValidationService : IWorkflowValidationService
{
    private const string WorkflowFileName = "WORKFLOW.md";
    private static readonly SecretType[] RequiredSecretTypes =
    [
        SecretType.GitHubToken,
        SecretType.OpenAiApiKey,
    ];

    public WorkflowValidationResult Validate(WorkflowValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);

        ValidateSettings(request, errors);
        ValidateWorkflowSource(request, errors);
        ValidateSecretReferences(request, errors);

        if (errors.Count == 0)
        {
            return WorkflowValidationResult.Success;
        }

        return new WorkflowValidationResult(errors.ToDictionary(
            item => item.Key,
            item => item.Value.ToArray(),
            StringComparer.Ordinal));
    }

    private static void ValidateSettings(
        WorkflowValidationRequest request,
        Dictionary<string, List<string>> errors)
    {
        if (!Enum.IsDefined(request.ExecutionMode))
        {
            AddError(errors, nameof(request.ExecutionMode), $"ExecutionMode value '{request.ExecutionMode}' is not supported.");
        }

        if (request.ExecutionMode is ExecutionMode.AzureContainer)
        {
            AddError(errors, nameof(request.ExecutionMode), "Azure Container workflow provisioning is not supported yet.");
        }

        if (request.Port is null)
        {
            AddError(errors, nameof(request.Port), "A port is required before provisioning a generated workflow.");
        }
        else if (request.Port is < 1 or > 65535)
        {
            AddError(errors, nameof(request.Port), "A TCP port must be between 1 and 65535.");
        }

        ValidatePath(request.WorkflowPath, nameof(request.WorkflowPath), mustEndWithWorkflowFile: true, errors);
        ValidatePath(request.DataPath, nameof(request.DataPath), mustEndWithWorkflowFile: false, errors);
    }

    private static void ValidateWorkflowSource(
        WorkflowValidationRequest request,
        Dictionary<string, List<string>> errors)
    {
        string source = request.WorkflowProfile.WorkflowSource;

        RequireSourceToken(source, request.Repository.Owner, "workflow repository owner", errors);
        RequireSourceToken(source, request.Repository.Name, "workflow repository name", errors);
        RequireSourceToken(source, "$GITHUB_TOKEN", "GitHub token environment reference", errors);

        if (ContainsKnownSecretLiteral(source))
        {
            AddError(
                errors,
                nameof(request.WorkflowProfile.WorkflowSource),
                "Workflow source must reference secret environment variables and must not inline credential values.");
        }
    }

    private static void ValidateSecretReferences(
        WorkflowValidationRequest request,
        Dictionary<string, List<string>> errors)
    {
        foreach (SecretType secretType in RequiredSecretTypes)
        {
            WorkflowSecretReference? reference = request.SecretReferences
                .FirstOrDefault(secretReference => secretReference.SecretType == secretType);
            string fieldName = $"{nameof(request.SecretReferences)}.{secretType}";
            string label = SecretTypeMetadata.Get(secretType).Label;

            if (reference is null)
            {
                AddError(errors, fieldName, $"{label} is required before provisioning a generated workflow.");
                continue;
            }

            if (reference.InheritanceMode is CredentialInheritanceMode.None)
            {
                AddError(errors, fieldName, $"{label} cannot be disabled for generated workflow provisioning.");
                continue;
            }

            if (!reference.IsResolved)
            {
                AddError(errors, fieldName, $"{label} must resolve to a stored secret before provisioning.");
            }
        }
    }

    private static void ValidatePath(
        string value,
        string fieldName,
        bool mustEndWithWorkflowFile,
        Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, fieldName, "A path is required before provisioning a generated workflow.");
            return;
        }

        string trimmed = value.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            AddError(errors, fieldName, "Path contains invalid characters.");
        }

        if (mustEndWithWorkflowFile &&
            !trimmed.EndsWith(WorkflowFileName, StringComparison.OrdinalIgnoreCase))
        {
            AddError(errors, fieldName, $"Workflow path must point to {WorkflowFileName}.");
        }
    }

    private static void RequireSourceToken(
        string source,
        string expectedToken,
        string label,
        Dictionary<string, List<string>> errors)
    {
        if (!source.Contains(expectedToken, StringComparison.Ordinal))
        {
            AddError(errors, nameof(WorkflowValidationRequest.WorkflowProfile), $"Generated workflow is missing {label}.");
        }
    }

    private static bool ContainsKnownSecretLiteral(string source) =>
        source.Contains("github_pat_", StringComparison.Ordinal) ||
        source.Contains("ghp_", StringComparison.Ordinal) ||
        source.Contains("sk-", StringComparison.Ordinal);

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string fieldName,
        string message)
    {
        if (!errors.TryGetValue(fieldName, out List<string>? messages))
        {
            messages = [];
            errors[fieldName] = messages;
        }

        messages.Add(message);
    }
}
