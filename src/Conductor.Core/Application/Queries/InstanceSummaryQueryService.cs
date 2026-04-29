using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

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
    DateTimeOffset? LatestSnapshotCapturedAtUtc);
