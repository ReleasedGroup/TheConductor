using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.Queries;

public interface IDashboardQueryService
{
    Task<DashboardProjection> GetDashboardAsync(
        DashboardQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record DashboardQuery(ProjectId? ProjectId = null);

public sealed record DashboardProjection(
    FleetMetricProjection Metrics,
    IReadOnlyList<HealthBucketProjection> HealthBuckets,
    IReadOnlyList<RepositoryListItemProjection> ActiveRepositories,
    IReadOnlyList<InstanceSummaryProjection> InstanceSummaries);

public sealed record FleetMetricProjection(
    int ManagedRepositoryCount,
    int HealthyRepositoryCount,
    int ActiveAgentCount,
    int BlockedIssueCount,
    int OpenPullRequestCount,
    decimal EstimatedSpendToday);

public sealed record HealthBucketProjection(InstanceHealthStatus Status, int Count);
