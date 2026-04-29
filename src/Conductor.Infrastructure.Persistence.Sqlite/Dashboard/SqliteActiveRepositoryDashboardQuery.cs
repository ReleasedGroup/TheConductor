using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using DomainEvent = Conductor.Core.Domain.Events.Event;

namespace Conductor.Infrastructure.Persistence.Sqlite.Dashboard;

public sealed class SqliteActiveRepositoryDashboardQuery : IActiveRepositoryDashboardQuery
{
    private const string UnassignedProjectName = "Unassigned";

    private readonly ConductorDbContext dbContext;

    public SqliteActiveRepositoryDashboardQuery(ConductorDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ActiveRepositoryDashboard> LoadAsync(CancellationToken cancellationToken = default)
    {
        List<Repository> repositories = await dbContext.Repositories
            .AsNoTracking()
            .Where(repository => !repository.IsArchived)
            .OrderBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToListAsync(cancellationToken);

        if (repositories.Count == 0)
        {
            return new ActiveRepositoryDashboard([]);
        }

        RepositoryId[] repositoryIds = [.. repositories.Select(repository => repository.Id)];

        Dictionary<ProjectId, Project> projectsById = await LoadProjectsAsync(
            repositories.Select(repository => repository.ProjectId),
            cancellationToken);
        Dictionary<RepositoryId, InstanceAggregate> instancesByRepository = await LoadInstanceAggregatesAsync(
            repositoryIds,
            cancellationToken);
        Dictionary<RepositoryId, IssueAggregate> issuesByRepository = await LoadIssueAggregatesAsync(
            repositoryIds,
            cancellationToken);
        Dictionary<RepositoryId, RunAggregate> runsByRepository = await LoadRunAggregatesAsync(
            repositoryIds,
            cancellationToken);
        Dictionary<RepositoryId, EventAggregate> eventsByRepository = await LoadEventAggregatesAsync(
            repositoryIds,
            cancellationToken);

        ActiveRepositoryDashboardRow[] rows =
        [
            .. repositories
                .Select(repository =>
                {
                    Project? project = null;
                    if (repository.ProjectId is { } projectId)
                    {
                        projectsById.TryGetValue(projectId, out project);
                    }

                    instancesByRepository.TryGetValue(repository.Id, out InstanceAggregate? instanceAggregate);
                    issuesByRepository.TryGetValue(repository.Id, out IssueAggregate? issueAggregate);
                    runsByRepository.TryGetValue(repository.Id, out RunAggregate? runAggregate);
                    eventsByRepository.TryGetValue(repository.Id, out EventAggregate? eventAggregate);

                    DateTimeOffset? lastActivityAtUtc = Latest(
                        repository.LastSyncedAtUtc,
                        instanceAggregate?.LastActivityAtUtc,
                        issueAggregate?.LastActivityAtUtc,
                        runAggregate?.LastActivityAtUtc,
                        eventAggregate?.LastActivityAtUtc);

                    return new ActiveRepositoryDashboardRow(
                        project?.Name ?? UnassignedProjectName,
                        repository.FullName.Value,
                        instanceAggregate?.Health ?? DashboardRepositoryHealth.Unknown,
                        issueAggregate?.ActiveIssueCount ?? 0,
                        instanceAggregate?.RunningAgentCount ?? 0,
                        runAggregate?.FailedRunCount ?? 0,
                        runAggregate?.OpenPullRequestCount ?? 0,
                        lastActivityAtUtc);
                })
                .OrderBy(row => HealthSortOrder(row.Health))
                .ThenByDescending(row => row.LastActivityAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(row => row.RepositoryFullName, StringComparer.OrdinalIgnoreCase),
        ];

        return new ActiveRepositoryDashboard(rows);
    }

    private async Task<Dictionary<ProjectId, Project>> LoadProjectsAsync(
        IEnumerable<ProjectId?> projectIds,
        CancellationToken cancellationToken)
    {
        ProjectId[] ids =
        [
            .. projectIds
                .Where(projectId => projectId.HasValue)
                .Select(projectId => projectId!.Value)
                .Distinct(),
        ];

        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext.Projects
            .AsNoTracking()
            .Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, cancellationToken);
    }

    private async Task<Dictionary<RepositoryId, InstanceAggregate>> LoadInstanceAggregatesAsync(
        RepositoryId[] repositoryIds,
        CancellationToken cancellationToken)
    {
        List<SymphonyInstance> instances = await dbContext.SymphonyInstances
            .AsNoTracking()
            .Where(instance => repositoryIds.Contains(instance.RepositoryId)
                && instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed)
            .ToListAsync(cancellationToken);

        if (instances.Count == 0)
        {
            return [];
        }

        Dictionary<SymphonyInstanceId, DateTimeOffset> latestSnapshotByInstanceId =
            await LoadLatestSnapshotTimesByInstanceAsync(
                [.. instances.Select(instance => instance.Id)],
                cancellationToken);

        return instances
            .GroupBy(instance => instance.RepositoryId)
            .ToDictionary(
                group => group.Key,
                group => new InstanceAggregate(
                    ComputeRepositoryHealth(group.Select(instance => instance.HealthStatus)),
                    group.Count(instance => instance.LifecycleStatus == InstanceLifecycleStatus.Running),
                    Latest(group.Select(instance => Latest(
                        instance.LastSeenAtUtc,
                        instance.LastHealthCheckAtUtc,
                        instance.LastStartedAtUtc,
                        instance.CreatedAtUtc,
                        latestSnapshotByInstanceId.TryGetValue(instance.Id, out DateTimeOffset capturedAtUtc)
                            ? capturedAtUtc
                            : null)))));
    }

    private async Task<Dictionary<RepositoryId, IssueAggregate>> LoadIssueAggregatesAsync(
        RepositoryId[] repositoryIds,
        CancellationToken cancellationToken)
    {
        List<TrackedIssue> issues = await dbContext.TrackedIssues
            .AsNoTracking()
            .Where(issue => repositoryIds.Contains(issue.RepositoryId)
                && issue.State == TrackedIssueState.Open
                && issue.SymphonyStatus != SymphonyIssueStatus.Succeeded)
            .ToListAsync(cancellationToken);

        return issues
            .GroupBy(issue => issue.RepositoryId)
            .ToDictionary(
                group => group.Key,
                group => new IssueAggregate(
                    group.Key,
                    group.Count(),
                    group.Max(issue => issue.LastActivityAtUtc)));
    }

    private async Task<Dictionary<RepositoryId, RunAggregate>> LoadRunAggregatesAsync(
        RepositoryId[] repositoryIds,
        CancellationToken cancellationToken)
    {
        List<Run> runs = await dbContext.Runs
            .AsNoTracking()
            .Where(run => repositoryIds.Contains(run.RepositoryId))
            .ToListAsync(cancellationToken);

        return runs
            .GroupBy(run => run.RepositoryId)
            .ToDictionary(
                group => group.Key,
                group => new RunAggregate(
                    group.Key,
                    group.Count(run => run.Status == RunStatus.Failed),
                    group
                        .Where(run => run.PullRequestUrl is not null)
                        .Select(run => run.PullRequestUrl!.AbsoluteUri)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Latest(group.Select(run => Latest(run.StartedAtUtc, run.FinishedAtUtc)))));
    }

    private async Task<Dictionary<RepositoryId, EventAggregate>> LoadEventAggregatesAsync(
        RepositoryId[] repositoryIds,
        CancellationToken cancellationToken)
    {
        List<DomainEvent> events = await dbContext.Events
            .AsNoTracking()
            .Where(eventRecord => eventRecord.RepositoryId.HasValue
                && repositoryIds.Contains(eventRecord.RepositoryId.Value))
            .ToListAsync(cancellationToken);

        return events
            .GroupBy(eventRecord => eventRecord.RepositoryId!.Value)
            .ToDictionary(
                group => group.Key,
                group => new EventAggregate(
                    group.Key,
                    group.Max(eventRecord => eventRecord.OccurredAtUtc)));
    }

    private async Task<Dictionary<SymphonyInstanceId, DateTimeOffset>> LoadLatestSnapshotTimesByInstanceAsync(
        SymphonyInstanceId[] instanceIds,
        CancellationToken cancellationToken)
    {
        List<InstanceSnapshot> snapshots = await dbContext.InstanceSnapshots
            .AsNoTracking()
            .Where(snapshot => instanceIds.Contains(snapshot.SymphonyInstanceId))
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.SymphonyInstanceId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(snapshot => snapshot.CapturedAtUtc));
    }

    private static DashboardRepositoryHealth ComputeRepositoryHealth(IEnumerable<InstanceHealthStatus> healthStatuses)
    {
        InstanceHealthStatus[] statuses = [.. healthStatuses];

        if (statuses.Length == 0)
        {
            return DashboardRepositoryHealth.Unknown;
        }

        if (statuses.Contains(InstanceHealthStatus.Critical))
        {
            return DashboardRepositoryHealth.Critical;
        }

        if (statuses.Contains(InstanceHealthStatus.Warning))
        {
            return DashboardRepositoryHealth.Warning;
        }

        if (statuses.Contains(InstanceHealthStatus.Offline))
        {
            return DashboardRepositoryHealth.Offline;
        }

        if (statuses.All(status => status == InstanceHealthStatus.Healthy))
        {
            return DashboardRepositoryHealth.Healthy;
        }

        return DashboardRepositoryHealth.Unknown;
    }

    private static int HealthSortOrder(DashboardRepositoryHealth health) =>
        health switch
        {
            DashboardRepositoryHealth.Critical => 0,
            DashboardRepositoryHealth.Warning => 1,
            DashboardRepositoryHealth.Offline => 2,
            DashboardRepositoryHealth.Unknown => 3,
            DashboardRepositoryHealth.Healthy => 4,
            _ => 5,
        };

    private static DateTimeOffset? Latest(params DateTimeOffset?[] candidates) =>
        Latest((IEnumerable<DateTimeOffset?>)candidates);

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> candidates)
    {
        DateTimeOffset? latest = null;

        foreach (DateTimeOffset? candidate in candidates)
        {
            if (candidate is not null && (latest is null || candidate > latest))
            {
                latest = candidate;
            }
        }

        return latest;
    }

    private sealed record InstanceAggregate(
        DashboardRepositoryHealth Health,
        int RunningAgentCount,
        DateTimeOffset? LastActivityAtUtc);

    private sealed record IssueAggregate(
        RepositoryId RepositoryId,
        int ActiveIssueCount,
        DateTimeOffset LastActivityAtUtc);

    private sealed record RunAggregate(
        RepositoryId RepositoryId,
        int FailedRunCount,
        int OpenPullRequestCount,
        DateTimeOffset? LastActivityAtUtc);

    private sealed record EventAggregate(RepositoryId RepositoryId, DateTimeOffset LastActivityAtUtc);
}
