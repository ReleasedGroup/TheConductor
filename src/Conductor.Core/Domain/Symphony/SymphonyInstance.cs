using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Symphony;

public sealed class SymphonyInstance
{
    public SymphonyInstance(
        SymphonyInstanceId id,
        RepositoryId repositoryId,
        string displayName,
        ExecutionMode executionMode,
        Uri baseUrl,
        DateTimeOffset createdAtUtc,
        InstanceLifecycleStatus lifecycleStatus = InstanceLifecycleStatus.NotProvisioned,
        InstanceHealthStatus healthStatus = InstanceHealthStatus.Unknown,
        int? port = null,
        string? containerName = null,
        string? azureResourceId = null,
        string? symphonyVersion = null,
        string? symphonyReleaseTag = null,
        Uri? symphonyArtifactSourceUrl = null,
        string? symphonyArtifactChecksum = null,
        SecretId? gitHubCredentialSecretId = null,
        CredentialInheritanceMode gitHubCredentialInheritanceMode = CredentialInheritanceMode.InheritDefault,
        SecretId? openAiCredentialSecretId = null,
        CredentialInheritanceMode openAiCredentialInheritanceMode = CredentialInheritanceMode.InheritDefault,
        string? workflowPath = null,
        string? dataPath = null,
        DateTimeOffset? lastStartedAtUtc = null,
        DateTimeOffset? lastSeenAtUtc = null)
    {
        Id = new SymphonyInstanceId(Guard.NotEmpty(id.Value, nameof(id)));
        RepositoryId = new RepositoryId(Guard.NotEmpty(repositoryId.Value, nameof(repositoryId)));
        DisplayName = Guard.NotWhiteSpace(displayName, nameof(displayName));
        ExecutionMode = executionMode;
        BaseUrl = Guard.AbsoluteUri(baseUrl, nameof(baseUrl));
        CreatedAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        LifecycleStatus = lifecycleStatus;
        HealthStatus = healthStatus;
        Port = Guard.Port(port, nameof(port));
        ContainerName = Guard.OptionalTrimmed(containerName);
        AzureResourceId = Guard.OptionalTrimmed(azureResourceId);
        SymphonyVersion = Guard.OptionalTrimmed(symphonyVersion);
        SymphonyReleaseTag = Guard.OptionalTrimmed(symphonyReleaseTag);
        SymphonyArtifactSourceUrl = Guard.OptionalAbsoluteUri(symphonyArtifactSourceUrl, nameof(symphonyArtifactSourceUrl));
        SymphonyArtifactChecksum = Guard.OptionalTrimmed(symphonyArtifactChecksum);
        GitHubCredentialSecretId = ValidateSecretReference(
            gitHubCredentialSecretId,
            gitHubCredentialInheritanceMode,
            nameof(gitHubCredentialSecretId));
        GitHubCredentialInheritanceMode = gitHubCredentialInheritanceMode;
        OpenAiCredentialSecretId = ValidateSecretReference(
            openAiCredentialSecretId,
            openAiCredentialInheritanceMode,
            nameof(openAiCredentialSecretId));
        OpenAiCredentialInheritanceMode = openAiCredentialInheritanceMode;
        WorkflowPath = Guard.OptionalTrimmed(workflowPath);
        DataPath = Guard.OptionalTrimmed(dataPath);
        LastStartedAtUtc = ValidateObservedAt(lastStartedAtUtc, nameof(lastStartedAtUtc));
        LastSeenAtUtc = ValidateObservedAt(lastSeenAtUtc, nameof(lastSeenAtUtc));
    }

    public SymphonyInstanceId Id { get; }

    public RepositoryId RepositoryId { get; }

    public string DisplayName { get; private set; }

    public ExecutionMode ExecutionMode { get; }

    public Uri BaseUrl { get; private set; }

    public int? Port { get; private set; }

    public string? ContainerName { get; private set; }

    public string? AzureResourceId { get; private set; }

    public InstanceLifecycleStatus LifecycleStatus { get; private set; }

    public InstanceHealthStatus HealthStatus { get; private set; }

    public string? SymphonyVersion { get; private set; }

    public string? SymphonyReleaseTag { get; private set; }

    public Uri? SymphonyArtifactSourceUrl { get; private set; }

    public string? SymphonyArtifactChecksum { get; private set; }

    public SecretId? GitHubCredentialSecretId { get; private set; }

    public CredentialInheritanceMode GitHubCredentialInheritanceMode { get; private set; }

    public SecretId? OpenAiCredentialSecretId { get; private set; }

    public CredentialInheritanceMode OpenAiCredentialInheritanceMode { get; private set; }

    public string? WorkflowPath { get; private set; }

    public string? DataPath { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? LastStartedAtUtc { get; private set; }

    public DateTimeOffset? LastHealthCheckAtUtc { get; private set; }

    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    public void Rename(string displayName)
    {
        DisplayName = Guard.NotWhiteSpace(displayName, nameof(displayName));
    }

    public void ConfigureEndpoint(Uri baseUrl, int? port)
    {
        var validatedBaseUrl = Guard.AbsoluteUri(baseUrl, nameof(baseUrl));
        var validatedPort = Guard.Port(port, nameof(port));

        BaseUrl = validatedBaseUrl;
        Port = validatedPort;
    }

    public void ConfigureRuntimeIdentity(string? containerName, string? azureResourceId)
    {
        ContainerName = Guard.OptionalTrimmed(containerName);
        AzureResourceId = Guard.OptionalTrimmed(azureResourceId);
    }

    public void ConfigureRelease(
        string? symphonyVersion,
        string? symphonyReleaseTag,
        Uri? symphonyArtifactSourceUrl,
        string? symphonyArtifactChecksum)
    {
        var validatedSymphonyVersion = Guard.OptionalTrimmed(symphonyVersion);
        var validatedSymphonyReleaseTag = Guard.OptionalTrimmed(symphonyReleaseTag);
        var validatedSymphonyArtifactSourceUrl = Guard.OptionalAbsoluteUri(
            symphonyArtifactSourceUrl,
            nameof(symphonyArtifactSourceUrl));
        var validatedSymphonyArtifactChecksum = Guard.OptionalTrimmed(symphonyArtifactChecksum);

        SymphonyVersion = validatedSymphonyVersion;
        SymphonyReleaseTag = validatedSymphonyReleaseTag;
        SymphonyArtifactSourceUrl = validatedSymphonyArtifactSourceUrl;
        SymphonyArtifactChecksum = validatedSymphonyArtifactChecksum;
    }

    public void ConfigureCredentials(
        SecretId? gitHubCredentialSecretId,
        CredentialInheritanceMode gitHubCredentialInheritanceMode,
        SecretId? openAiCredentialSecretId,
        CredentialInheritanceMode openAiCredentialInheritanceMode)
    {
        var validatedGitHubSecretId = ValidateSecretReference(
            gitHubCredentialSecretId,
            gitHubCredentialInheritanceMode,
            nameof(gitHubCredentialSecretId));
        var validatedOpenAiSecretId = ValidateSecretReference(
            openAiCredentialSecretId,
            openAiCredentialInheritanceMode,
            nameof(openAiCredentialSecretId));

        GitHubCredentialSecretId = validatedGitHubSecretId;
        GitHubCredentialInheritanceMode = gitHubCredentialInheritanceMode;
        OpenAiCredentialSecretId = validatedOpenAiSecretId;
        OpenAiCredentialInheritanceMode = openAiCredentialInheritanceMode;
    }

    public void ConfigurePaths(string? workflowPath, string? dataPath)
    {
        WorkflowPath = Guard.OptionalTrimmed(workflowPath);
        DataPath = Guard.OptionalTrimmed(dataPath);
    }

    public void MarkLifecycle(InstanceLifecycleStatus lifecycleStatus)
    {
        LifecycleStatus = lifecycleStatus;
    }

    public void RecordStarted(DateTimeOffset startedAtUtc)
    {
        LastStartedAtUtc = ValidateObservedAt(startedAtUtc, nameof(startedAtUtc));
        LifecycleStatus = InstanceLifecycleStatus.Running;
    }

    public void RecordHealth(InstanceHealthStatus healthStatus, DateTimeOffset observedAtUtc)
    {
        var validatedObservedAtUtc = ValidateObservedAt(observedAtUtc, nameof(observedAtUtc));

        HealthStatus = healthStatus;
        LastHealthCheckAtUtc = validatedObservedAtUtc;

        if (healthStatus is not InstanceHealthStatus.Offline and not InstanceHealthStatus.Unknown)
        {
            LastSeenAtUtc = validatedObservedAtUtc;
        }
    }

    private static SecretId? ValidateSecretReference(
        SecretId? secretId,
        CredentialInheritanceMode inheritanceMode,
        string parameterName)
    {
        if (inheritanceMode == CredentialInheritanceMode.SpecificSecret)
        {
            if (secretId is null)
            {
                throw new ArgumentException("A specific secret reference requires a secret id.", parameterName);
            }

            return new SecretId(Guard.NotEmpty(secretId.Value.Value, parameterName));
        }

        if (secretId is not null)
        {
            throw new ArgumentException("Secret ids can only be set for specific secret references.", parameterName);
        }

        return null;
    }

    private DateTimeOffset? ValidateObservedAt(DateTimeOffset? observedAtUtc, string parameterName)
    {
        if (observedAtUtc is null)
        {
            return null;
        }

        var utcObservedAt = Guard.Utc(observedAtUtc.Value, parameterName);

        if (utcObservedAt < CreatedAtUtc)
        {
            throw new ArgumentException("Observed timestamp cannot be earlier than created timestamp.", parameterName);
        }

        return utcObservedAt;
    }
}
