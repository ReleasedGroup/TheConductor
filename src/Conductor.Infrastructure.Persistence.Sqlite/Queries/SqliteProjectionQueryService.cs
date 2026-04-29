using System.Text.Json;
using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Queries;

public sealed class SqliteProjectionQueryService :
    IDashboardQueryService,
    IRepositoryListQueryService,
    IProjectListQueryService,
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
        RepositoryId[] repositoryIds = repositories
            .Select(repository => repository.Id)
            .ToArray();
        int blockedIssueCount = await CountBlockedIssuesAsync(repositoryIds, cancellationToken);

        FleetMetricProjection metrics = new(
            ManagedRepositoryCount: repositories.Count,
            HealthyRepositoryCount: repositories.Count(repository =>
                repository.WorstHealthStatus == InstanceHealthStatus.Healthy),
            ActiveAgentCount: instances.Count(instance =>
                instance.LifecycleStatus == InstanceLifecycleStatus.Running),
            BlockedIssueCount: blockedIssueCount,
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
        IQueryable<Repository> repositories = dbContext.Repositories.AsNoTracking();

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

        List<Repository> repositoryRows = await repositories
            .OrderBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToListAsync(cancellationToken);

        Dictionary<ProjectId, Project> projectsById = await LoadProjectsAsync(
            repositoryRows.Select(repository => repository.ProjectId),
            cancellationToken);
        Dictionary<RepositoryId, List<SymphonyInstance>> instancesByRepositoryId =
            await LoadActiveInstancesByRepositoryAsync(
                repositoryRows.Select(repository => repository.Id),
                cancellationToken);

        return repositoryRows
            .Select(repository =>
            {
                List<SymphonyInstance> instances = instancesByRepositoryId.GetValueOrDefault(repository.Id) ?? [];
                InstanceHealthStatus worstHealthStatus = instances.Count == 0
                    ? InstanceHealthStatus.Unknown
                    : instances
                        .Select(instance => instance.HealthStatus)
                        .OrderByDescending(MapHealthSeverity)
                        .First();

                Project? project = null;
                if (repository.ProjectId is { } repositoryProjectId)
                {
                    projectsById.TryGetValue(repositoryProjectId, out project);
                }

                return new RepositoryListItemProjection(
                    repository.Id,
                    repository.ProjectId,
                    project?.Name,
                    repository.Provider,
                    repository.Owner,
                    repository.Name,
                    repository.FullName.Value,
                    repository.DefaultBranch,
                    repository.CloneUrl,
                    repository.WebUrl,
                    repository.Visibility,
                    repository.IsArchived,
                    repository.LastSyncedAtUtc,
                    repository.OrchestrationStatus,
                    repository.OrchestrationStatusReason,
                    instances.Count,
                    instances.Count(instance => instance.LifecycleStatus == InstanceLifecycleStatus.Running),
                    worstHealthStatus,
                    instances.Select(instance => instance.LastHealthCheckAtUtc).Max());
            })
            .OrderBy(repository => repository.ProjectName)
            .ThenBy(repository => repository.Owner)
            .ThenBy(repository => repository.Name)
            .ToArray();
    }

    public async Task<RepositoryDetailProjection?> GetRepositoryAsync(
        RepositoryId repositoryId,
        CancellationToken cancellationToken = default)
    {
        Repository? repository = await dbContext.Repositories
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == repositoryId, cancellationToken);

        if (repository is null)
        {
            return null;
        }

        Dictionary<ProjectId, Project> projectsById = await LoadProjectsAsync(
            [repository.ProjectId],
            cancellationToken);
        Dictionary<RepositoryId, List<SymphonyInstance>> instancesByRepositoryId =
            await LoadActiveInstancesByRepositoryAsync([repository.Id], cancellationToken);
        List<SymphonyInstance> instances = instancesByRepositoryId.GetValueOrDefault(repository.Id) ?? [];
        InstanceHealthStatus worstHealthStatus = instances.Count == 0
            ? InstanceHealthStatus.Unknown
            : instances
                .Select(instance => instance.HealthStatus)
                .OrderByDescending(MapHealthSeverity)
                .First();

        Project? project = null;
        if (repository.ProjectId is { } projectId)
        {
            projectsById.TryGetValue(projectId, out project);
        }

        return new RepositoryDetailProjection(
            repository.Id,
            repository.ProjectId,
            project?.Name,
            repository.Provider,
            repository.Owner,
            repository.Name,
            repository.FullName.Value,
            repository.DefaultBranch,
            repository.CloneUrl,
            repository.WebUrl,
            repository.Visibility,
            repository.IsArchived,
            repository.LastSyncedAtUtc,
            repository.OrchestrationStatus,
            repository.OrchestrationStatusReason,
            instances.Count,
            instances.Count(instance => instance.LifecycleStatus == InstanceLifecycleStatus.Running),
            worstHealthStatus,
            instances.Select(instance => instance.LastHealthCheckAtUtc).Max());
    }

    public async Task<IReadOnlyList<ProjectListItemProjection>> ListProjectsAsync(
        ProjectListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Project> projects = dbContext.Projects.AsNoTracking();

        if (!query.IncludeArchived)
        {
            projects = projects.Where(project => project.Status != ProjectStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            projects = projects.Where(project =>
                project.Name.Contains(search) ||
                project.OwnerName.Contains(search));
        }

        return await projects
            .OrderBy(project => project.Name)
            .ThenBy(project => project.OwnerName)
            .Select(project => new ProjectListItemProjection(
                project.Id,
                project.Name,
                project.OwnerName,
                project.Status))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
        InstanceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Repository> repositories = dbContext.Repositories.AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            repositories = repositories.Where(repository => repository.ProjectId == projectId);
        }

        if (query.RepositoryId is { } repositoryId)
        {
            repositories = repositories.Where(repository => repository.Id == repositoryId);
        }

        List<Repository> repositoryRows = await repositories.ToListAsync(cancellationToken);
        RepositoryId[] repositoryIds = repositoryRows.Select(repository => repository.Id).ToArray();

        IQueryable<SymphonyInstance> instances = dbContext.SymphonyInstances
            .AsNoTracking()
            .Where(instance => repositoryIds.Contains(instance.RepositoryId));

        if (!query.IncludeDestroyed)
        {
            instances = instances.Where(instance => instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed);
        }

        List<SymphonyInstance> instanceRows = await instances
            .OrderBy(instance => instance.DisplayName)
            .ToListAsync(cancellationToken);

        Dictionary<RepositoryId, Repository> repositoriesById = repositoryRows.ToDictionary(repository => repository.Id);
        Dictionary<ProjectId, Project> projectsById = await LoadProjectsAsync(
            repositoryRows.Select(repository => repository.ProjectId),
            cancellationToken);
        Dictionary<SymphonyInstanceId, LatestInstanceSnapshot> latestSnapshotByInstanceId = await LoadLatestSnapshotsByInstanceAsync(
            instanceRows.Select(instance => instance.Id),
            cancellationToken);
        Dictionary<SecretId, SecretDescriptor> secretDescriptorsById = await LoadSecretDescriptorsAsync(
            instanceRows
                .SelectMany(instance => new[]
                {
                    instance.GitHubCredentialSecretId,
                    instance.OpenAiCredentialSecretId,
                })
                .Where(secretId => secretId.HasValue)
                .Select(secretId => secretId!.Value),
            cancellationToken);

        return instanceRows
            .Select(instance =>
            {
                Repository repository = repositoriesById[instance.RepositoryId];
                Project? project = null;
                if (repository.ProjectId is { } repositoryProjectId)
                {
                    projectsById.TryGetValue(repositoryProjectId, out project);
                }

                latestSnapshotByInstanceId.TryGetValue(instance.Id, out LatestInstanceSnapshot? latestSnapshot);

                return new InstanceSummaryProjection(
                    instance.Id,
                    instance.RepositoryId,
                    repository.FullName.Value,
                    repository.ProjectId,
                    project?.Name,
                    instance.DisplayName,
                    instance.ExecutionMode,
                    instance.BaseUrl,
                    instance.LifecycleStatus,
                    instance.HealthStatus,
                    instance.LastHealthCheckAtUtc,
                    instance.LastSeenAtUtc,
                    latestSnapshot?.CapturedAtUtc,
                    instance.SymphonyVersion ?? latestSnapshot?.RuntimeMetadata.Version,
                    instance.SymphonyReleaseTag,
                    latestSnapshot?.RuntimeMetadata.WorkflowOwner,
                    latestSnapshot?.RuntimeMetadata.WorkflowRepository,
                    latestSnapshot?.RuntimeMetadata.WorkflowSourcePath,
                    latestSnapshot?.ActiveIssueCount ?? 0,
                    latestSnapshot?.RunningSessionCount ?? 0,
                    latestSnapshot?.RetryQueueCount ?? 0,
                    latestSnapshot?.FailedRunCount ?? 0,
                    latestSnapshot?.TokenTotal ?? 0,
                    instance.GitHubCredentialInheritanceMode,
                    instance.GitHubCredentialSecretId,
                    GetSecretName(secretDescriptorsById, instance.GitHubCredentialSecretId),
                    instance.OpenAiCredentialInheritanceMode,
                    instance.OpenAiCredentialSecretId,
                    GetSecretName(secretDescriptorsById, instance.OpenAiCredentialSecretId));
            })
            .OrderBy(instance => instance.RepositoryFullName)
            .ThenBy(instance => instance.DisplayName)
            .ToArray();
    }

    private async Task<Dictionary<ProjectId, Project>> LoadProjectsAsync(
        IEnumerable<ProjectId?> projectIds,
        CancellationToken cancellationToken)
    {
        ProjectId[] ids = projectIds
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext.Projects
            .AsNoTracking()
            .Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, cancellationToken);
    }

    private async Task<int> CountBlockedIssuesAsync(
        IReadOnlyCollection<RepositoryId> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (repositoryIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.TrackedIssues
            .AsNoTracking()
            .CountAsync(issue => repositoryIds.Contains(issue.RepositoryId) && issue.IsBlocked, cancellationToken);
    }

    private async Task<Dictionary<RepositoryId, List<SymphonyInstance>>> LoadActiveInstancesByRepositoryAsync(
        IEnumerable<RepositoryId> repositoryIds,
        CancellationToken cancellationToken)
    {
        RepositoryId[] ids = repositoryIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        List<SymphonyInstance> instances = await dbContext.SymphonyInstances
            .AsNoTracking()
            .Where(instance =>
                ids.Contains(instance.RepositoryId) &&
                instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed)
            .ToListAsync(cancellationToken);

        return instances
            .GroupBy(instance => instance.RepositoryId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());
    }

    private async Task<Dictionary<SymphonyInstanceId, LatestInstanceSnapshot>> LoadLatestSnapshotsByInstanceAsync(
        IEnumerable<SymphonyInstanceId> instanceIds,
        CancellationToken cancellationToken)
    {
        SymphonyInstanceId[] ids = instanceIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        List<InstanceSnapshot> snapshots = await dbContext.InstanceSnapshots
            .AsNoTracking()
            .Where(snapshot => ids.Contains(snapshot.SymphonyInstanceId))
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.SymphonyInstanceId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    InstanceSnapshot snapshot = group
                        .OrderByDescending(candidate => candidate.CapturedAtUtc)
                        .First();

                    return new LatestInstanceSnapshot(
                        snapshot.CapturedAtUtc,
                        TryReadRuntimeMetadata(snapshot.RuntimeJson),
                        snapshot.ActiveIssueCount,
                        snapshot.RunningSessionCount,
                        snapshot.RetryQueueCount,
                        snapshot.FailedRunCount,
                        snapshot.TokenTotal);
                });
    }

    private async Task<Dictionary<SecretId, SecretDescriptor>> LoadSecretDescriptorsAsync(
        IEnumerable<SecretId> secretIds,
        CancellationToken cancellationToken)
    {
        SecretId[] ids = secretIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext.SecretDescriptors
            .AsNoTracking()
            .Where(descriptor => ids.Contains(descriptor.Id))
            .ToDictionaryAsync(descriptor => descriptor.Id, cancellationToken);
    }

    private static string? GetSecretName(
        IReadOnlyDictionary<SecretId, SecretDescriptor> secretDescriptorsById,
        SecretId? secretId)
    {
        return secretId is not null && secretDescriptorsById.TryGetValue(secretId.Value, out SecretDescriptor? descriptor)
            ? descriptor.Name
            : null;
    }

    private static RuntimeMetadata TryReadRuntimeMetadata(string? runtimeJson)
    {
        if (string.IsNullOrWhiteSpace(runtimeJson))
        {
            return RuntimeMetadata.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(runtimeJson);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return RuntimeMetadata.Empty;
            }

            JsonElement root = document.RootElement;
            string? version = ReadStringProperty(root, "version");
            string? workflowOwner = null;
            string? workflowRepository = null;
            string? workflowSourcePath = null;

            if (TryGetObjectProperty(root, "workflow", out JsonElement workflow))
            {
                workflowOwner = ReadStringProperty(workflow, "owner");
                workflowRepository = ReadStringProperty(workflow, "repository");
                workflowSourcePath = ReadStringProperty(workflow, "sourcePath")
                    ?? ReadStringProperty(workflow, "path");
            }

            return new RuntimeMetadata(
                version,
                workflowOwner,
                workflowRepository,
                workflowSourcePath);
        }
        catch (JsonException)
        {
            return RuntimeMetadata.Empty;
        }
    }

    private static bool TryGetObjectProperty(
        JsonElement element,
        string propertyName,
        out JsonElement propertyValue)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if ((property.NameEquals(propertyName)
                    || string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                propertyValue = property.Value;
                return true;
            }
        }

        propertyValue = default;
        return false;
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName)
                || string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : null;
            }
        }

        return null;
    }

    private static int MapHealthSeverity(InstanceHealthStatus status) => status switch
    {
        InstanceHealthStatus.Healthy => 0,
        InstanceHealthStatus.Unknown => 1,
        InstanceHealthStatus.Warning => 2,
        InstanceHealthStatus.Critical => 3,
        InstanceHealthStatus.Offline => 4,
        _ => 1,
    };

    private sealed record LatestInstanceSnapshot(
        DateTimeOffset CapturedAtUtc,
        RuntimeMetadata RuntimeMetadata,
        int ActiveIssueCount,
        int RunningSessionCount,
        int RetryQueueCount,
        int FailedRunCount,
        long TokenTotal);

    private sealed record RuntimeMetadata(
        string? Version,
        string? WorkflowOwner,
        string? WorkflowRepository,
        string? WorkflowSourcePath)
    {
        public static RuntimeMetadata Empty { get; } = new(null, null, null, null);
    }
}
