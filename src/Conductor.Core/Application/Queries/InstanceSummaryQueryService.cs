using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Application.Queries;

public interface IInstanceSummaryQueryService
{
    Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
        InstanceSummaryQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record InstanceSummaryQuery(
    ProjectId? ProjectId = null,
    RepositoryId? RepositoryId = null,
    bool IncludeDestroyed = false);

public sealed record InstanceSummaryProjection(
    SymphonyInstanceId Id,
    RepositoryId RepositoryId,
    string RepositoryFullName,
    ProjectId? ProjectId,
    string? ProjectName,
    string DisplayName,
    ExecutionMode ExecutionMode,
    Uri BaseUrl,
    InstanceLifecycleStatus LifecycleStatus,
    InstanceHealthStatus HealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? LatestSnapshotCapturedAtUtc,
    string? SymphonyVersion = null,
    string? SymphonyReleaseTag = null,
    string? WorkflowOwner = null,
    string? WorkflowRepository = null,
    string? WorkflowSourcePath = null,
    int ActiveIssueCount = 0,
    int RunningSessionCount = 0,
    int RetryQueueCount = 0,
    int FailedRunCount = 0,
    long TokenTotal = 0,
    CredentialInheritanceMode GitHubCredentialInheritanceMode = CredentialInheritanceMode.InheritDefault,
    SecretId? GitHubCredentialSecretId = null,
    string? GitHubCredentialName = null,
    CredentialInheritanceMode OpenAiCredentialInheritanceMode = CredentialInheritanceMode.InheritDefault,
    SecretId? OpenAiCredentialSecretId = null,
    string? OpenAiCredentialName = null);
