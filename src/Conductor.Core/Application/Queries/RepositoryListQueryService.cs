using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.Queries;

public interface IRepositoryListQueryService
{
    Task<IReadOnlyList<RepositoryListItemProjection>> ListRepositoriesAsync(
        RepositoryListQuery query,
        CancellationToken cancellationToken = default);

    Task<RepositoryDetailProjection?> GetRepositoryAsync(
        RepositoryId repositoryId,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryListQuery(
    ProjectId? ProjectId = null,
    string? Search = null,
    bool IncludeArchived = false);

public sealed record RepositoryListItemProjection(
    RepositoryId Id,
    ProjectId? ProjectId,
    string? ProjectName,
    RepositoryProvider Provider,
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    Uri CloneUrl,
    Uri WebUrl,
    RepositoryVisibility Visibility,
    bool IsArchived,
    DateTimeOffset? LastSyncedAtUtc,
    RepositoryOrchestrationStatus OrchestrationStatus,
    string? OrchestrationStatusReason,
    int InstanceCount,
    int RunningInstanceCount,
    InstanceHealthStatus WorstHealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc);

public sealed record RepositoryDetailProjection(
    RepositoryId Id,
    ProjectId? ProjectId,
    string? ProjectName,
    RepositoryProvider Provider,
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    Uri CloneUrl,
    Uri WebUrl,
    RepositoryVisibility Visibility,
    bool IsArchived,
    DateTimeOffset? LastSyncedAtUtc,
    RepositoryOrchestrationStatus OrchestrationStatus,
    string? OrchestrationStatusReason,
    int InstanceCount,
    int RunningInstanceCount,
    InstanceHealthStatus WorstHealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc);
