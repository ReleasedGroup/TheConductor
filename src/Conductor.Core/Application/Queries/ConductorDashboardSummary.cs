using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.Queries;

public sealed record ConductorDashboardSummary(
    int ProjectCount,
    int RepositoryCount,
    int InstanceCount,
    int NeedsAttentionCount,
    IReadOnlyList<RepositoryOverview> ActiveRepositories,
    IReadOnlyList<RepositoryOverview> NeedsAttention);

public sealed record RepositoryOverview(
    RepositoryId RepositoryId,
    string FullName,
    string ProjectName,
    string DefaultBranch,
    string WebUrl,
    ExecutionMode? ExecutionMode,
    InstanceLifecycleStatus? LifecycleStatus,
    InstanceHealthStatus? HealthStatus,
    string? InstanceBaseUrl,
    DateTimeOffset? LastSeenAtUtc);
