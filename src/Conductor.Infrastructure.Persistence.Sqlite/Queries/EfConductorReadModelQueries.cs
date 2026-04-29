using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Infrastructure.Persistence.Sqlite.Schema;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Queries;

public sealed class EfConductorReadModelQueries(IDbContextFactory<ConductorDbContext> dbContextFactory)
    : IConductorReadModelQueries
{
    public async Task<ConductorDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RepositoryOverview> repositories = await ListRepositoriesAsync(cancellationToken);

        await using ConductorDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        int projectCount = await dbContext.Set<ProjectRecord>().CountAsync(cancellationToken);
        int instanceCount = await dbContext.Set<SymphonyInstanceRecord>().CountAsync(cancellationToken);
        int needsAttentionCount = repositories.Count(NeedsAttention);

        return new ConductorDashboardSummary(
            projectCount,
            repositories.Count,
            instanceCount,
            needsAttentionCount,
            repositories
                .Where(repository => repository.LifecycleStatus is InstanceLifecycleStatus.Running)
                .OrderBy(repository => repository.FullName, StringComparer.Ordinal)
                .Take(6)
                .ToArray(),
            repositories
                .Where(NeedsAttention)
                .OrderBy(repository => repository.FullName, StringComparer.Ordinal)
                .ToArray());
    }

    public async Task<IReadOnlyList<RepositoryOverview>> ListRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        await using ConductorDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Dictionary<string, string> projectNames = await dbContext.Set<ProjectRecord>()
            .AsNoTracking()
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);

        List<RepositoryRecord> repositories = await dbContext.Set<RepositoryRecord>()
            .AsNoTracking()
            .OrderBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToListAsync(cancellationToken);

        List<SymphonyInstanceRecord> instances = await dbContext.Set<SymphonyInstanceRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return repositories
            .Select(repository => ToOverview(repository, projectNames, instances))
            .ToArray();
    }

    public async Task<RepositoryOverview?> GetRepositoryAsync(
        RepositoryId repositoryId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RepositoryOverview> repositories = await ListRepositoriesAsync(cancellationToken);

        return repositories.SingleOrDefault(repository => repository.RepositoryId == repositoryId);
    }

    private static RepositoryOverview ToOverview(
        RepositoryRecord repository,
        IReadOnlyDictionary<string, string> projectNames,
        IReadOnlyList<SymphonyInstanceRecord> instances)
    {
        SymphonyInstanceRecord? instance = instances.SingleOrDefault(candidate => candidate.RepositoryId == repository.Id);
        string projectName = repository.ProjectId is { } projectId && projectNames.TryGetValue(projectId, out string? name)
            ? name
            : "Unassigned";

        return new RepositoryOverview(
            new RepositoryId(Guid.Parse(repository.Id)),
            $"{repository.Owner}/{repository.Name}",
            projectName,
            repository.DefaultBranch,
            repository.WebUrl,
            ParseEnum<ExecutionMode>(instance?.ExecutionMode),
            ParseEnum<InstanceLifecycleStatus>(instance?.Status),
            ParseEnum<InstanceHealthStatus>(instance?.HealthStatus),
            instance?.BaseUrl,
            instance?.LastSeenAtUtc);
    }

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out TEnum result) ? result : null;

    private static bool NeedsAttention(RepositoryOverview repository) =>
        repository.HealthStatus is InstanceHealthStatus.Warning or InstanceHealthStatus.Critical or InstanceHealthStatus.Offline ||
        repository.LifecycleStatus is InstanceLifecycleStatus.Failed;
}
