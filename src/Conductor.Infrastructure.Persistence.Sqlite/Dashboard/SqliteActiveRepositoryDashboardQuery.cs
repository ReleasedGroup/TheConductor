using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Persistence.Sqlite.Schema;
using Microsoft.EntityFrameworkCore;

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
        List<RepositoryProjection> repositories = await dbContext
            .Set<RepositoryRecord>()
            .AsNoTracking()
            .GroupJoin(
                dbContext.Set<ProjectRecord>().AsNoTracking(),
                repository => repository.ProjectId,
                project => project.Id,
                (repository, projects) => new { repository, projects })
            .SelectMany(
                joined => joined.projects.DefaultIfEmpty(),
                (joined, project) => new RepositoryProjection(
                    joined.repository.Id,
                    project == null ? UnassignedProjectName : project.Name,
                    joined.repository.Owner + "/" + joined.repository.Name,
                    joined.repository.OpenIssueCount,
                    joined.repository.PullRequestCount,
                    joined.repository.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        if (repositories.Count == 0)
        {
            return new ActiveRepositoryDashboard([]);
        }

        string[] repositoryIds = [.. repositories.Select(repository => repository.Id)];

        Dictionary<string, InstanceAggregate> instancesByRepository = await LoadInstanceAggregatesAsync(
            repositoryIds,
            cancellationToken);
        Dictionary<string, IssueAggregate> issuesByRepository = await LoadIssueAggregatesAsync(
            repositoryIds,
            cancellationToken);
        Dictionary<string, RunAggregate> runsByRepository = await LoadRunAggregatesAsync(
            repositoryIds,
            cancellationToken);
        Dictionary<string, EventAggregate> eventsByRepository = await LoadEventAggregatesAsync(
            repositoryIds,
            cancellationToken);

        ActiveRepositoryDashboardRow[] rows =
        [
            .. repositories
                .Select(repository =>
                {
                    instancesByRepository.TryGetValue(repository.Id, out InstanceAggregate? instanceAggregate);
                    issuesByRepository.TryGetValue(repository.Id, out IssueAggregate? issueAggregate);
                    runsByRepository.TryGetValue(repository.Id, out RunAggregate? runAggregate);
                    eventsByRepository.TryGetValue(repository.Id, out EventAggregate? eventAggregate);

                    DateTimeOffset? lastActivityAtUtc = Latest(
                        repository.UpdatedAtUtc,
                        instanceAggregate?.LastActivityAtUtc,
                        issueAggregate?.LastActivityAtUtc,
                        runAggregate?.LastActivityAtUtc,
                        eventAggregate?.LastActivityAtUtc);

                    return new ActiveRepositoryDashboardRow(
                        repository.ProjectName,
                        repository.RepositoryFullName,
                        instanceAggregate?.Health ?? DashboardRepositoryHealth.Unknown,
                        issueAggregate?.ActiveIssueCount ?? repository.OpenIssueCount,
                        instanceAggregate?.RunningAgentCount ?? 0,
                        runAggregate?.FailedRunCount ?? 0,
                        repository.OpenPullRequestCount,
                        lastActivityAtUtc);
                })
                .OrderBy(row => HealthSortOrder(row.Health))
                .ThenByDescending(row => row.LastActivityAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(row => row.RepositoryFullName, StringComparer.OrdinalIgnoreCase),
        ];

        return new ActiveRepositoryDashboard(rows);
    }

    private async Task<Dictionary<string, InstanceAggregate>> LoadInstanceAggregatesAsync(
        string[] repositoryIds,
        CancellationToken cancellationToken)
    {
        var instances = await dbContext
            .Set<SymphonyInstanceRecord>()
            .AsNoTracking()
            .Where(instance => repositoryIds.Contains(instance.RepositoryId))
            .Select(instance => new
            {
                instance.RepositoryId,
                instance.Status,
                instance.HealthStatus,
                instance.LastSeenAtUtc,
                instance.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return instances
            .GroupBy(instance => instance.RepositoryId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new InstanceAggregate(
                    ComputeRepositoryHealth(group.Select(instance => instance.HealthStatus)),
                    group.Count(instance => string.Equals(instance.Status, InstanceLifecycleStatus.Running.ToString(), StringComparison.Ordinal)),
                    Latest(group.SelectMany(instance => new DateTimeOffset?[] { instance.LastSeenAtUtc, instance.UpdatedAtUtc }))),
                StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, IssueAggregate>> LoadIssueAggregatesAsync(
        string[] repositoryIds,
        CancellationToken cancellationToken)
    {
        var issues = await dbContext
            .Set<TrackedIssueRecord>()
            .AsNoTracking()
            .Where(issue => repositoryIds.Contains(issue.RepositoryId)
                && issue.SymphonyStatus != SymphonyIssueStatus.Succeeded.ToString())
            .Select(issue => new
            {
                issue.RepositoryId,
                issue.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        List<IssueAggregate> issueAggregates =
        [
            .. issues
                .GroupBy(issue => issue.RepositoryId, StringComparer.Ordinal)
                .Select(group => new IssueAggregate(
                    group.Key,
                    group.Count(),
                    group.Max(issue => issue.UpdatedAtUtc))),
        ];

        return issueAggregates.ToDictionary(aggregate => aggregate.RepositoryId, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, RunAggregate>> LoadRunAggregatesAsync(
        string[] repositoryIds,
        CancellationToken cancellationToken)
    {
        var runs = await dbContext
            .Set<RunRecord>()
            .AsNoTracking()
            .Where(run => repositoryIds.Contains(run.RepositoryId))
            .Select(run => new
            {
                run.RepositoryId,
                run.Status,
                run.StartedAtUtc,
                run.CompletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        List<RunAggregate> runAggregates =
        [
            .. runs
                .GroupBy(run => run.RepositoryId, StringComparer.Ordinal)
                .Select(group => new RunAggregate(
                    group.Key,
                    group.Count(run => run.Status == RunStatus.Failed.ToString()),
                    Latest(group.SelectMany(run => new DateTimeOffset?[] { run.StartedAtUtc, run.CompletedAtUtc })))),
        ];

        return runAggregates.ToDictionary(aggregate => aggregate.RepositoryId, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, EventAggregate>> LoadEventAggregatesAsync(
        string[] repositoryIds,
        CancellationToken cancellationToken)
    {
        var events = await dbContext
            .Set<EventRecord>()
            .AsNoTracking()
            .Where(eventRecord => eventRecord.RepositoryId != null && repositoryIds.Contains(eventRecord.RepositoryId!))
            .Select(eventRecord => new
            {
                RepositoryId = eventRecord.RepositoryId!,
                eventRecord.OccurredAtUtc,
            })
            .ToListAsync(cancellationToken);

        List<EventAggregate> eventAggregates =
        [
            .. events
                .GroupBy(eventRecord => eventRecord.RepositoryId, StringComparer.Ordinal)
                .Select(group => new EventAggregate(
                    group.Key,
                    group.Max(eventRecord => eventRecord.OccurredAtUtc))),
        ];

        return eventAggregates.ToDictionary(aggregate => aggregate.RepositoryId, StringComparer.Ordinal);
    }

    private static DashboardRepositoryHealth ComputeRepositoryHealth(IEnumerable<string> healthStatuses)
    {
        string[] statuses = [.. healthStatuses];

        if (statuses.Length == 0)
        {
            return DashboardRepositoryHealth.Unknown;
        }

        if (statuses.Contains(InstanceHealthStatus.Critical.ToString(), StringComparer.Ordinal))
        {
            return DashboardRepositoryHealth.Critical;
        }

        if (statuses.Contains(InstanceHealthStatus.Warning.ToString(), StringComparer.Ordinal))
        {
            return DashboardRepositoryHealth.Warning;
        }

        if (statuses.Contains(InstanceHealthStatus.Offline.ToString(), StringComparer.Ordinal))
        {
            return DashboardRepositoryHealth.Offline;
        }

        if (statuses.All(status => string.Equals(status, InstanceHealthStatus.Healthy.ToString(), StringComparison.Ordinal)))
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

    private sealed record RepositoryProjection(
        string Id,
        string ProjectName,
        string RepositoryFullName,
        int OpenIssueCount,
        int OpenPullRequestCount,
        DateTimeOffset UpdatedAtUtc);

    private sealed record InstanceAggregate(
        DashboardRepositoryHealth Health,
        int RunningAgentCount,
        DateTimeOffset? LastActivityAtUtc);

    private sealed record IssueAggregate(
        string RepositoryId,
        int ActiveIssueCount,
        DateTimeOffset LastActivityAtUtc);

    private sealed record RunAggregate(
        string RepositoryId,
        int FailedRunCount,
        DateTimeOffset? LastActivityAtUtc);

    private sealed record EventAggregate(string RepositoryId, DateTimeOffset LastActivityAtUtc);
}
