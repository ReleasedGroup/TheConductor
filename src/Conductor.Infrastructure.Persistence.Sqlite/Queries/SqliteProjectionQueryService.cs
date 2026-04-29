using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Infrastructure.Persistence.Sqlite.Schema;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Queries;

public sealed class SqliteProjectionQueryService :
    IDashboardQueryService,
    IRepositoryListQueryService,
    IInstanceSummaryQueryService
{
    private readonly ConductorDbContext dbContext;

    public SqliteProjectionQueryService(ConductorDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<DashboardProjection> GetDashboardAsync(
        DashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        RepositoryListQuery repositoryQuery = new(query.ProjectId);
        InstanceSummaryQuery instanceQuery = new(query.ProjectId);

        IReadOnlyList<RepositoryListItemProjection> repositories =
            await ListRepositoriesAsync(repositoryQuery, cancellationToken);
        IReadOnlyList<InstanceSummaryProjection> instances =
            await ListInstanceSummariesAsync(instanceQuery, cancellationToken);
        string[] repositoryIds = repositories
            .Select(repository => FormatId(repository.Id.Value))
            .ToArray();
        int blockedIssueCount = await CountBlockedIssuesAsync(repositoryIds, cancellationToken);
        int openPullRequestCount = await CountOpenPullRequestsAsync(repositoryIds, cancellationToken);

        FleetMetricProjection metrics = new(
            ManagedRepositoryCount: repositories.Count,
            HealthyRepositoryCount: repositories.Count(repository =>
                repository.WorstHealthStatus == InstanceHealthStatus.Healthy),
            ActiveAgentCount: instances.Count(instance =>
                instance.LifecycleStatus == InstanceLifecycleStatus.Running),
            BlockedIssueCount: blockedIssueCount,
            OpenPullRequestCount: openPullRequestCount,
            EstimatedSpendToday: 0m);

        IReadOnlyList<HealthBucketProjection> healthBuckets = Enum
            .GetValues<InstanceHealthStatus>()
            .Select(status => new HealthBucketProjection(
                status,
                instances.Count(instance => instance.HealthStatus == status)))
            .ToArray();

        return new DashboardProjection(
            metrics,
            healthBuckets,
            repositories,
            instances);
    }

    public async Task<IReadOnlyList<RepositoryListItemProjection>> ListRepositoriesAsync(
        RepositoryListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<RepositoryRecord> repositories = dbContext.Set<RepositoryRecord>().AsNoTracking();

        if (!query.IncludeArchived)
        {
            repositories = repositories.Where(repository => !repository.IsArchived);
        }

        if (query.ProjectId is { } projectId)
        {
            string projectIdValue = FormatId(projectId.Value);
            repositories = repositories.Where(repository => repository.ProjectId == projectIdValue);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            repositories = repositories.Where(repository =>
                repository.Owner.Contains(search) ||
                repository.Name.Contains(search));
        }

        List<RepositoryRecord> repositoryRows = await repositories
            .OrderBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToListAsync(cancellationToken);

        Dictionary<string, ProjectRecord> projectsById = await LoadProjectsAsync(
            repositoryRows.Select(repository => repository.ProjectId),
            cancellationToken);
        Dictionary<string, List<SymphonyInstanceRecord>> instancesByRepositoryId =
            await LoadActiveInstancesByRepositoryAsync(
                repositoryRows.Select(repository => repository.Id),
                cancellationToken);

        return repositoryRows
            .Select(repository =>
            {
                List<SymphonyInstanceRecord> instances = instancesByRepositoryId.GetValueOrDefault(repository.Id) ?? [];
                InstanceHealthStatus worstHealthStatus = instances.Count == 0
                    ? InstanceHealthStatus.Unknown
                    : instances
                        .Select(instance => ParseEnum<InstanceHealthStatus>(instance.HealthStatus))
                        .OrderByDescending(MapHealthSeverity)
                        .First();

                projectsById.TryGetValue(repository.ProjectId ?? string.Empty, out ProjectRecord? project);

                return new RepositoryListItemProjection(
                    ParseRepositoryId(repository.Id),
                    ParseProjectId(repository.ProjectId),
                    project?.Name,
                    ParseEnum<RepositoryProvider>(repository.Provider),
                    repository.Owner,
                    repository.Name,
                    $"{repository.Owner}/{repository.Name}",
                    repository.DefaultBranch,
                    new Uri(repository.WebUrl, UriKind.Absolute),
                    repository.IsArchived,
                    instances.Count,
                    instances.Count(instance => ParseEnum<InstanceLifecycleStatus>(instance.Status) == InstanceLifecycleStatus.Running),
                    worstHealthStatus,
                    instances.Select(instance => instance.LastHealthCheckAtUtc).Max());
            })
            .OrderBy(repository => repository.ProjectName)
            .ThenBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToArray();
    }

    public async Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
        InstanceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<RepositoryRecord> repositories = dbContext.Set<RepositoryRecord>().AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            string projectIdValue = FormatId(projectId.Value);
            repositories = repositories.Where(repository => repository.ProjectId == projectIdValue);
        }

        if (query.RepositoryId is { } repositoryId)
        {
            string repositoryIdValue = FormatId(repositoryId.Value);
            repositories = repositories.Where(repository => repository.Id == repositoryIdValue);
        }

        List<RepositoryRecord> repositoryRows = await repositories.ToListAsync(cancellationToken);
        string[] repositoryIds = repositoryRows.Select(repository => repository.Id).ToArray();

        IQueryable<SymphonyInstanceRecord> instances = dbContext.Set<SymphonyInstanceRecord>()
            .AsNoTracking()
            .Where(instance => repositoryIds.Contains(instance.RepositoryId));

        if (!query.IncludeDestroyed)
        {
            instances = instances.Where(instance => instance.Status != nameof(InstanceLifecycleStatus.Destroyed));
        }

        List<SymphonyInstanceRecord> instanceRows = await instances
            .OrderBy(instance => instance.DisplayName)
            .ToListAsync(cancellationToken);

        Dictionary<string, RepositoryRecord> repositoriesById = repositoryRows.ToDictionary(
            repository => repository.Id,
            StringComparer.Ordinal);
        Dictionary<string, ProjectRecord> projectsById = await LoadProjectsAsync(
            repositoryRows.Select(repository => repository.ProjectId),
            cancellationToken);
        Dictionary<string, DateTimeOffset> latestSnapshotByInstanceId = await LoadLatestSnapshotsByInstanceAsync(
            instanceRows.Select(instance => instance.Id),
            cancellationToken);

        return instanceRows
            .Select(instance =>
            {
                RepositoryRecord repository = repositoriesById[instance.RepositoryId];
                projectsById.TryGetValue(repository.ProjectId ?? string.Empty, out ProjectRecord? project);
                latestSnapshotByInstanceId.TryGetValue(instance.Id, out DateTimeOffset latestSnapshotAtUtc);

                return new InstanceSummaryProjection(
                    ParseSymphonyInstanceId(instance.Id),
                    ParseRepositoryId(instance.RepositoryId),
                    $"{repository.Owner}/{repository.Name}",
                    ParseProjectId(repository.ProjectId),
                    project?.Name,
                    instance.DisplayName,
                    ParseEnum<ExecutionMode>(instance.ExecutionMode),
                    new Uri(instance.BaseUrl, UriKind.Absolute),
                    ParseEnum<InstanceLifecycleStatus>(instance.Status),
                    ParseEnum<InstanceHealthStatus>(instance.HealthStatus),
                    instance.LastHealthCheckAtUtc,
                    instance.LastSeenAtUtc,
                    latestSnapshotAtUtc == default ? null : latestSnapshotAtUtc);
            })
            .OrderBy(instance => instance.RepositoryFullName)
            .ThenBy(instance => instance.DisplayName)
            .ToArray();
    }

    private async Task<Dictionary<string, ProjectRecord>> LoadProjectsAsync(
        IEnumerable<string?> projectIds,
        CancellationToken cancellationToken)
    {
        string[] ids = projectIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, ProjectRecord>(StringComparer.Ordinal);
        }

        return await dbContext.Set<ProjectRecord>()
            .AsNoTracking()
            .Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, StringComparer.Ordinal, cancellationToken);
    }

    private async Task<int> CountBlockedIssuesAsync(
        IReadOnlyCollection<string> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.Set<TrackedIssueRecord>()
            .AsNoTracking()
            .CountAsync(issue => repositoryIds.Contains(issue.RepositoryId) && issue.IsBlocked, cancellationToken);
    }

    private async Task<int> CountOpenPullRequestsAsync(
        IReadOnlyCollection<string> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.Set<RepositoryRecord>()
            .AsNoTracking()
            .Where(repository => repositoryIds.Contains(repository.Id))
            .SumAsync(repository => repository.PullRequestCount, cancellationToken);
    }

    private async Task<Dictionary<string, List<SymphonyInstanceRecord>>> LoadActiveInstancesByRepositoryAsync(
        IEnumerable<string> repositoryIds,
        CancellationToken cancellationToken)
    {
        string[] ids = repositoryIds.Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, List<SymphonyInstanceRecord>>(StringComparer.Ordinal);
        }

        List<SymphonyInstanceRecord> instances = await dbContext.Set<SymphonyInstanceRecord>()
            .AsNoTracking()
            .Where(instance =>
                ids.Contains(instance.RepositoryId) &&
                instance.Status != nameof(InstanceLifecycleStatus.Destroyed))
            .ToListAsync(cancellationToken);

        return instances
            .GroupBy(instance => instance.RepositoryId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, DateTimeOffset>> LoadLatestSnapshotsByInstanceAsync(
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken)
    {
        string[] ids = instanceIds.Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        }

        List<InstanceSnapshotRecord> snapshots = await dbContext.Set<InstanceSnapshotRecord>()
            .AsNoTracking()
            .Where(snapshot => ids.Contains(snapshot.SymphonyInstanceId))
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.SymphonyInstanceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(snapshot => snapshot.CapturedAtUtc),
                StringComparer.Ordinal);
    }

    private static string FormatId(Guid id) => id.ToString("D");

    private static ProjectId? ParseProjectId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : new ProjectId(Guid.Parse(id));

    private static RepositoryId ParseRepositoryId(string id) => new(Guid.Parse(id));

    private static SymphonyInstanceId ParseSymphonyInstanceId(string id) => new(Guid.Parse(id));

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value);

    private static int MapHealthSeverity(InstanceHealthStatus status) => status switch
    {
        InstanceHealthStatus.Healthy => 0,
        InstanceHealthStatus.Unknown => 1,
        InstanceHealthStatus.Warning => 2,
        InstanceHealthStatus.Critical => 3,
        InstanceHealthStatus.Offline => 4,
        _ => 1,
    };
}
