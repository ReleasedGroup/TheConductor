using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
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

        FleetMetricProjection metrics = new(
            ManagedRepositoryCount: repositories.Count,
            HealthyRepositoryCount: repositories.Count(repository =>
                repository.WorstHealthStatus == InstanceHealthStatus.Healthy),
            ActiveAgentCount: instances.Count(instance =>
                instance.LifecycleStatus == InstanceLifecycleStatus.Running),
            BlockedIssueCount: 0,
            OpenPullRequestCount: 0,
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
        IQueryable<Core.Domain.Repositories.Repository> repositories = dbContext.Repositories.AsNoTracking();

        if (!query.IncludeArchived)
        {
            repositories = repositories.Where(repository => !repository.IsArchived);
        }

        if (query.ProjectId is { } projectId)
        {
            repositories = repositories.Where(repository => repository.ProjectId == projectId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            repositories = repositories.Where(repository =>
                repository.Owner.Contains(search) ||
                repository.Name.Contains(search));
        }

        var rows = await (
            from repository in repositories
            join project in dbContext.Projects.AsNoTracking()
                on repository.ProjectId equals project.Id into projectJoin
            from project in projectJoin.DefaultIfEmpty()
            join instance in dbContext.SymphonyInstances.AsNoTracking()
                on repository.Id equals instance.RepositoryId into instanceJoin
            select new
            {
                repository.Id,
                repository.ProjectId,
                ProjectName = project == null ? null : project.Name,
                repository.Provider,
                repository.Owner,
                repository.Name,
                repository.DefaultBranch,
                repository.WebUrl,
                repository.IsArchived,
                InstanceCount = instanceJoin.Count(instance =>
                    instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed),
                RunningInstanceCount = instanceJoin.Count(instance =>
                    instance.LifecycleStatus == InstanceLifecycleStatus.Running),
                WorstHealthSeverity = instanceJoin.Any(instance =>
                    instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed)
                    ? instanceJoin
                        .Where(instance => instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed)
                        .Max(instance =>
                            instance.HealthStatus == InstanceHealthStatus.Offline ? 4 :
                            instance.HealthStatus == InstanceHealthStatus.Critical ? 3 :
                            instance.HealthStatus == InstanceHealthStatus.Warning ? 2 :
                            instance.HealthStatus == InstanceHealthStatus.Unknown ? 1 : 0)
                    : 1,
                LastHealthCheckAtUtc = instanceJoin
                    .Where(instance => instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed)
                    .Max(instance => instance.LastHealthCheckAtUtc),
            })
            .OrderBy(row => row.ProjectName)
            .ThenBy(row => row.Owner)
            .ThenBy(row => row.Name)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new RepositoryListItemProjection(
                row.Id,
                row.ProjectId,
                row.ProjectName,
                row.Provider,
                row.Owner,
                row.Name,
                $"{row.Owner}/{row.Name}",
                row.DefaultBranch,
                row.WebUrl,
                row.IsArchived,
                row.InstanceCount,
                row.RunningInstanceCount,
                MapHealthSeverity(row.WorstHealthSeverity),
                row.LastHealthCheckAtUtc))
            .ToArray();
    }

    public async Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
        InstanceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Core.Domain.Symphony.SymphonyInstance> instances = dbContext.SymphonyInstances.AsNoTracking();

        if (!query.IncludeDestroyed)
        {
            instances = instances.Where(instance =>
                instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed);
        }

        if (query.RepositoryId is { } repositoryId)
        {
            instances = instances.Where(instance => instance.RepositoryId == repositoryId);
        }

        IQueryable<Core.Domain.Repositories.Repository> repositories = dbContext.Repositories.AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            repositories = repositories.Where(repository => repository.ProjectId == projectId);
        }

        var latestSnapshots =
            from snapshot in dbContext.InstanceSnapshots.AsNoTracking()
            group snapshot by snapshot.SymphonyInstanceId
            into snapshotGroup
            select new
            {
                SymphonyInstanceId = snapshotGroup.Key,
                CapturedAtUtc = snapshotGroup.Max(snapshot => snapshot.CapturedAtUtc),
            };

        var rows = await (
            from instance in instances
            join repository in repositories
                on instance.RepositoryId equals repository.Id
            join project in dbContext.Projects.AsNoTracking()
                on repository.ProjectId equals project.Id into projectJoin
            from project in projectJoin.DefaultIfEmpty()
            join latestSnapshot in latestSnapshots
                on instance.Id equals latestSnapshot.SymphonyInstanceId into latestSnapshotJoin
            from latestSnapshot in latestSnapshotJoin.DefaultIfEmpty()
            select new
            {
                instance.Id,
                instance.RepositoryId,
                RepositoryFullName = repository.Owner + "/" + repository.Name,
                repository.ProjectId,
                ProjectName = project == null ? null : project.Name,
                instance.DisplayName,
                instance.ExecutionMode,
                instance.BaseUrl,
                instance.LifecycleStatus,
                instance.HealthStatus,
                instance.LastHealthCheckAtUtc,
                instance.LastSeenAtUtc,
                LatestSnapshotCapturedAtUtc = latestSnapshot == null
                    ? null
                    : (DateTimeOffset?)latestSnapshot.CapturedAtUtc,
            })
            .OrderBy(row => row.RepositoryFullName)
            .ThenBy(row => row.DisplayName)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new InstanceSummaryProjection(
                row.Id,
                row.RepositoryId,
                row.RepositoryFullName,
                row.ProjectId,
                row.ProjectName,
                row.DisplayName,
                row.ExecutionMode,
                row.BaseUrl,
                row.LifecycleStatus,
                row.HealthStatus,
                row.LastHealthCheckAtUtc,
                row.LastSeenAtUtc,
                row.LatestSnapshotCapturedAtUtc))
            .ToArray();
    }

    private static InstanceHealthStatus MapHealthSeverity(int severity) => severity switch
    {
        0 => InstanceHealthStatus.Healthy,
        1 => InstanceHealthStatus.Unknown,
        2 => InstanceHealthStatus.Warning,
        3 => InstanceHealthStatus.Critical,
        4 => InstanceHealthStatus.Offline,
        _ => InstanceHealthStatus.Unknown,
    };
}
