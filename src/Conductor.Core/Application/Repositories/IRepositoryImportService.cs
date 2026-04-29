using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Common;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Releases;
using Conductor.Core.Domain.Repositories;

namespace Conductor.Core.Application.Repositories;

public interface IRepositoryImportService
{
    Task<RepositoryImportResult> ImportAsync(
        RepositoryImportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryImportRequest(
    string RepositoryFullName,
    string? DefaultBranch = null,
    string? CloneUrl = null,
    string? WebUrl = null,
    RepositoryVisibility Visibility = RepositoryVisibility.Public,
    bool IsArchived = false,
    ProjectId? ProjectId = null,
    bool CreateSymphonyInstance = false,
    string? InstanceDisplayName = null,
    ExecutionMode ExecutionMode = ExecutionMode.LocalProcess,
    string? InstanceBaseUrl = null,
    int? Port = null,
    string? ReleaseTag = null,
    WorkflowProfileId? WorkflowProfileId = null,
    CredentialInheritanceMode GitHubCredentialInheritanceMode = CredentialInheritanceMode.InheritDefault,
    SecretId? GitHubCredentialSecretId = null,
    CredentialInheritanceMode OpenAiCredentialInheritanceMode = CredentialInheritanceMode.InheritDefault,
    SecretId? OpenAiCredentialSecretId = null,
    string? WorkflowPath = null,
    string? DataPath = null,
    string? RequestedByUserId = null)
{
    public static RepositoryImportRequest FromGitHubRepositorySummary(
        GitHubRepositorySummary repository,
        ProjectId? projectId = null,
        bool createSymphonyInstance = false)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return new RepositoryImportRequest(
            repository.FullName,
            DefaultBranch: repository.DefaultBranch,
            CloneUrl: repository.CloneUrl.AbsoluteUri,
            WebUrl: repository.WebUrl.AbsoluteUri,
            Visibility: repository.Visibility,
            IsArchived: repository.IsArchived,
            ProjectId: projectId,
            CreateSymphonyInstance: createSymphonyInstance);
    }
}

public sealed record RepositoryImportResult(
    string RepositoryId,
    string RepositoryFullName,
    bool CreatedRepository,
    string? SymphonyInstanceId,
    bool CreatedSymphonyInstance,
    string? SymphonyInstanceDisplayName,
    DateTimeOffset ImportedAtUtc,
    string? ProjectId = null,
    string? ProjectName = null);

public sealed class RepositoryImportValidationException : Exception
{
    public RepositoryImportValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Repository import failed validation.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed record RepositoryImportCredentialSelection
{
    public RepositoryImportCredentialSelection(
        CredentialInheritanceMode inheritanceMode,
        SecretId? secretId = null)
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
                throw new ArgumentException("A specific credential selection requires a secret id.", nameof(secretId));
            }

            SecretId = new SecretId(Guard.NotEmpty(secretId.Value.Value, nameof(secretId)));
        }
        else if (secretId is not null)
        {
            throw new ArgumentException("Secret ids can only be set for specific credential selections.", nameof(secretId));
        }

        InheritanceMode = inheritanceMode;
    }

    public CredentialInheritanceMode InheritanceMode { get; }

    public SecretId? SecretId { get; }
}

public sealed record RepositoryImportInstancePlan(
    string DisplayName,
    ExecutionMode ExecutionMode,
    Uri BaseUrl,
    int? Port,
    ReleaseSelector ReleaseSelector,
    WorkflowProfileId? WorkflowProfileId,
    RepositoryImportCredentialSelection GitHubCredential,
    RepositoryImportCredentialSelection OpenAiCredential,
    string? WorkflowPath,
    string? DataPath);

public sealed record RepositoryImportPlan(
    GitHubRepositoryFullName FullName,
    string DefaultBranch,
    Uri CloneUrl,
    Uri WebUrl,
    RepositoryVisibility Visibility,
    bool IsArchived,
    ProjectId? ProjectId,
    RepositoryImportInstancePlan? InstancePlan)
{
    private const string LatestReleaseAlias = "latest";

    public RepositoryOrchestrationStatus OrchestrationStatus =>
        IsArchived ? RepositoryOrchestrationStatus.Ineligible : RepositoryOrchestrationStatus.Eligible;

    public string? OrchestrationStatusReason =>
        IsArchived ? "Archived repositories cannot be orchestrated." : null;

    public static RepositoryImportPlan Create(RepositoryImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);

        GitHubRepositoryFullName? fullName = null;
        if (!GitHubRepositoryFullName.TryParse(request.RepositoryFullName, out fullName))
        {
            AddError(errors, nameof(request.RepositoryFullName), "Enter a GitHub repository full name in owner/name format.");
        }

        string defaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch)
            ? "main"
            : request.DefaultBranch.Trim();

        ValidateEnum(request.Visibility, nameof(request.Visibility), errors);
        ValidateNullableId(request.ProjectId, nameof(request.ProjectId), errors);

        Uri? cloneUrl = null;
        Uri? webUrl = null;
        if (fullName is not null)
        {
            cloneUrl = ParseOptionalAbsoluteUri(
                request.CloneUrl,
                nameof(request.CloneUrl),
                errors,
                new Uri($"https://github.com/{fullName.Value}.git", UriKind.Absolute));
            webUrl = ParseOptionalAbsoluteUri(
                request.WebUrl,
                nameof(request.WebUrl),
                errors,
                new Uri($"https://github.com/{fullName.Value}", UriKind.Absolute));
        }

        RepositoryImportInstancePlan? instancePlan = null;
        if (request.CreateSymphonyInstance)
        {
            instancePlan = CreateInstancePlan(request, fullName, errors);

            if (request.IsArchived)
            {
                AddError(errors, nameof(request.CreateSymphonyInstance), "Archived repositories cannot create a Symphony instance shell.");
            }
        }

        if (errors.Count > 0)
        {
            throw new RepositoryImportValidationException(ToErrorDictionary(errors));
        }

        return new RepositoryImportPlan(
            fullName!,
            defaultBranch,
            cloneUrl!,
            webUrl!,
            request.Visibility,
            request.IsArchived,
            request.ProjectId,
            instancePlan);
    }

    private static RepositoryImportInstancePlan? CreateInstancePlan(
        RepositoryImportRequest request,
        GitHubRepositoryFullName? fullName,
        Dictionary<string, List<string>> errors)
    {
        ValidateEnum(request.ExecutionMode, nameof(request.ExecutionMode), errors);
        ValidateNullableId(request.WorkflowProfileId, nameof(request.WorkflowProfileId), errors);
        ValidatePort(request.Port, nameof(request.Port), errors);

        Uri? baseUrl = ParseRequiredHttpUri(
            request.InstanceBaseUrl,
            nameof(request.InstanceBaseUrl),
            errors);
        ReleaseSelector releaseSelector = CreateReleaseSelector(request.ReleaseTag);
        RepositoryImportCredentialSelection? gitHubCredential = CreateCredentialSelection(
            request.GitHubCredentialInheritanceMode,
            request.GitHubCredentialSecretId,
            nameof(request.GitHubCredentialSecretId),
            errors);
        RepositoryImportCredentialSelection? openAiCredential = CreateCredentialSelection(
            request.OpenAiCredentialInheritanceMode,
            request.OpenAiCredentialSecretId,
            nameof(request.OpenAiCredentialSecretId),
            errors);

        if (baseUrl is null || gitHubCredential is null || openAiCredential is null)
        {
            return null;
        }

        int? port = request.Port ?? (baseUrl.IsDefaultPort ? null : baseUrl.Port);
        string displayName = string.IsNullOrWhiteSpace(request.InstanceDisplayName)
            ? fullName?.Value ?? "Imported repository"
            : request.InstanceDisplayName.Trim();

        return new RepositoryImportInstancePlan(
            displayName,
            request.ExecutionMode,
            baseUrl,
            port,
            releaseSelector,
            request.WorkflowProfileId,
            gitHubCredential,
            openAiCredential,
            Guard.OptionalTrimmed(request.WorkflowPath),
            Guard.OptionalTrimmed(request.DataPath));
    }

    private static RepositoryImportCredentialSelection? CreateCredentialSelection(
        CredentialInheritanceMode inheritanceMode,
        SecretId? secretId,
        string fieldName,
        Dictionary<string, List<string>> errors)
    {
        ValidateEnum(inheritanceMode, fieldName, errors);
        ValidateNullableId(secretId, fieldName, errors);

        try
        {
            return new RepositoryImportCredentialSelection(inheritanceMode, secretId);
        }
        catch (ArgumentException ex)
        {
            AddError(errors, fieldName, ex.Message);
            return null;
        }
    }

    private static ReleaseSelector CreateReleaseSelector(string? releaseTag)
    {
        string? normalized = Guard.OptionalTrimmed(releaseTag);

        return normalized is null || string.Equals(normalized, LatestReleaseAlias, StringComparison.OrdinalIgnoreCase)
            ? ReleaseSelector.Latest
            : ReleaseSelector.PinnedTag(normalized);
    }

    private static Uri? ParseOptionalAbsoluteUri(
        string? value,
        string fieldName,
        Dictionary<string, List<string>> errors,
        Uri fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        AddError(errors, fieldName, "Enter an absolute URL.");
        return null;
    }

    private static Uri? ParseRequiredHttpUri(
        string? value,
        string fieldName,
        Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, fieldName, "A Symphony instance base URL is required when creating an instance shell.");
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not "http" and not "https")
        {
            AddError(errors, fieldName, "Enter an absolute HTTP or HTTPS URL.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(uri.Query) || !string.IsNullOrWhiteSpace(uri.Fragment))
        {
            AddError(errors, fieldName, "The instance URL cannot include a query string or fragment.");
            return null;
        }

        return uri;
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        string fieldName,
        Dictionary<string, List<string>> errors)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            AddError(errors, fieldName, $"{typeof(TEnum).Name} value '{value}' is not supported.");
        }
    }

    private static void ValidateNullableId<TId>(
        TId? id,
        string fieldName,
        Dictionary<string, List<string>> errors)
        where TId : struct
    {
        if (id is null)
        {
            return;
        }

        Guid value = id switch
        {
            ProjectId projectId => projectId.Value,
            WorkflowProfileId workflowProfileId => workflowProfileId.Value,
            SecretId secretId => secretId.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Identifier type is not supported."),
        };

        if (value == Guid.Empty)
        {
            AddError(errors, fieldName, "A non-empty identifier is required.");
        }
    }

    private static void ValidatePort(
        int? port,
        string fieldName,
        Dictionary<string, List<string>> errors)
    {
        if (port is < 1 or > 65535)
        {
            AddError(errors, fieldName, "A TCP port must be between 1 and 65535.");
        }
    }

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

    private static IReadOnlyDictionary<string, string[]> ToErrorDictionary(
        Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(
            item => item.Key,
            item => item.Value.ToArray(),
            StringComparer.Ordinal);
    }
}
