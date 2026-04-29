using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Queries;

public sealed class EfConductorReadModelQueries(IDbContextFactory<ConductorDbContext> dbContextFactory)
    : IConductorReadModelQueries
{
    public async Task<ConductorDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RepositoryOverview> repositories = await ListRepositoriesAsync(cancellationToken);

        await using ConductorDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        int projectCount = await dbContext.Projects.CountAsync(cancellationToken);
        int instanceCount = await dbContext.SymphonyInstances.CountAsync(cancellationToken);
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

        Dictionary<ProjectId, string> projectNames = await dbContext.Projects
            .AsNoTracking()
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);

        List<Repository> repositories = await dbContext.Repositories
            .AsNoTracking()
            .OrderBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToListAsync(cancellationToken);

        List<SymphonyInstance> instances = await dbContext.SymphonyInstances
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
        Repository repository,
        IReadOnlyDictionary<ProjectId, string> projectNames,
        IReadOnlyList<SymphonyInstance> instances)
    {
        SymphonyInstance? instance = instances.SingleOrDefault(candidate => candidate.RepositoryId == repository.Id);
        string projectName = repository.ProjectId is { } projectId && projectNames.TryGetValue(projectId, out string? name)
            ? name
            : "Unassigned";

        return new RepositoryOverview(
            repository.Id,
            repository.FullName,
            projectName,
            repository.DefaultBranch,
            repository.WebUrl.ToString(),
            instance?.ExecutionMode,
            instance?.LifecycleStatus,
            instance?.HealthStatus,
            instance?.BaseUrl.ToString(),
            instance?.LastSeenAtUtc);
    }

    private static bool NeedsAttention(RepositoryOverview repository) =>
        repository.HealthStatus is InstanceHealthStatus.Warning or InstanceHealthStatus.Critical or InstanceHealthStatus.Offline ||
        repository.LifecycleStatus is InstanceLifecycleStatus.Failed;
}
