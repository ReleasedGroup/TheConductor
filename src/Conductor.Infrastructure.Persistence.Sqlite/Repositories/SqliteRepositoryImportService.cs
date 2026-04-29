using System.Text.Json;
using Conductor.Core.Application.Repositories;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class SqliteRepositoryImportService : IRepositoryImportService
{
    private readonly ConductorDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public SqliteRepositoryImportService(
        ConductorDbContext dbContext,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<RepositoryImportResult> ImportAsync(
        RepositoryImportRequest request,
        CancellationToken cancellationToken = default)
    {
        RepositoryImportPlan plan = RepositoryImportPlan.Create(request);
        RepositoryImportReferenceValidationResult referenceResult =
            await ValidateReferencesAsync(plan, cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        (Repository Repository, bool CreatedRepository) repositoryResult =
            await CreateOrUpdateRepositoryAsync(plan, now, cancellationToken);
        (SymphonyInstance? Instance, bool CreatedInstance) instanceResult =
            await CreateInstanceShellAsync(repositoryResult.Repository, plan, now, cancellationToken);

        string metadataJson = JsonSerializer.Serialize(new
        {
            repositoryFullName = plan.FullName.Value,
            projectId = plan.ProjectId?.ToString(),
            projectName = referenceResult.ProjectName,
            createdRepository = repositoryResult.CreatedRepository,
            createSymphonyInstance = plan.InstancePlan is not null,
            createdSymphonyInstance = instanceResult.CreatedInstance,
            symphonyInstanceId = instanceResult.Instance?.Id.ToString(),
            executionMode = plan.InstancePlan?.ExecutionMode.ToString(),
            releaseSelector = plan.InstancePlan?.ReleaseSelector.ToString(),
            workflowProfileId = plan.InstancePlan?.WorkflowProfileId?.ToString(),
        });

        dbContext.AuditEvents.Add(new AuditEvent(
            AuditEventId.New(),
            string.IsNullOrWhiteSpace(request.RequestedByUserId) ? "system" : request.RequestedByUserId.Trim(),
            "ImportRepository",
            "Repository",
            repositoryResult.Repository.Id.ToString(),
            now,
            AuditEventOutcome.Succeeded,
            correlationId: null,
            message: $"Imported repository {plan.FullName.Value}.",
            metadataJson: metadataJson));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RepositoryImportResult(
            repositoryResult.Repository.Id.ToString(),
            plan.FullName.Value,
            repositoryResult.CreatedRepository,
            instanceResult.Instance?.Id.ToString(),
            instanceResult.CreatedInstance,
            instanceResult.Instance?.DisplayName,
            now,
            plan.ProjectId?.ToString(),
            referenceResult.ProjectName);
    }

    private async Task<RepositoryImportReferenceValidationResult> ValidateReferencesAsync(
        RepositoryImportPlan plan,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);
        string? projectName = null;

        if (plan.ProjectId is { } projectId)
        {
            Project? project = await dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

            if (project is null)
            {
                AddError(errors, nameof(RepositoryImportRequest.ProjectId), "The selected project does not exist.");
            }
            else
            {
                projectName = project.Name;
            }
        }

        if (plan.InstancePlan?.WorkflowProfileId is { } workflowProfileId &&
            !await dbContext.WorkflowProfiles.AsNoTracking().AnyAsync(profile => profile.Id == workflowProfileId, cancellationToken))
        {
            AddError(errors, nameof(RepositoryImportRequest.WorkflowProfileId), "The selected workflow profile does not exist.");
        }

        if (plan.InstancePlan is { } instancePlan)
        {
            await ValidateSecretReferenceAsync(
                instancePlan.GitHubCredential,
                SecretType.GitHubToken,
                nameof(RepositoryImportRequest.GitHubCredentialSecretId),
                errors,
                cancellationToken);
            await ValidateSecretReferenceAsync(
                instancePlan.OpenAiCredential,
                SecretType.OpenAiApiKey,
                nameof(RepositoryImportRequest.OpenAiCredentialSecretId),
                errors,
                cancellationToken);
        }

        if (errors.Count > 0)
        {
            throw new RepositoryImportValidationException(ToErrorDictionary(errors));
        }

        return new RepositoryImportReferenceValidationResult(projectName);
    }

    private async Task ValidateSecretReferenceAsync(
        RepositoryImportCredentialSelection credential,
        SecretType expectedSecretType,
        string fieldName,
        Dictionary<string, List<string>> errors,
        CancellationToken cancellationToken)
    {
        if (credential.InheritanceMode != CredentialInheritanceMode.SpecificSecret)
        {
            return;
        }

        bool exists = await dbContext.SecretDescriptors
            .AsNoTracking()
            .AnyAsync(
                descriptor => descriptor.Id == credential.SecretId && descriptor.SecretType == expectedSecretType,
                cancellationToken);

        if (!exists)
        {
            AddError(errors, fieldName, $"The selected {expectedSecretType} secret does not exist.");
        }
    }

    private async Task<(Repository Repository, bool CreatedRepository)> CreateOrUpdateRepositoryAsync(
        RepositoryImportPlan plan,
        DateTimeOffset importedAtUtc,
        CancellationToken cancellationToken)
    {
        Repository? repository = await dbContext.Repositories
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Provider == RepositoryProvider.GitHub &&
                    candidate.Owner == plan.FullName.Owner &&
                    candidate.Name == plan.FullName.Name,
                cancellationToken);

        if (repository is null)
        {
            repository = new Repository(
                RepositoryId.New(),
                RepositoryProvider.GitHub,
                plan.FullName,
                plan.DefaultBranch,
                plan.CloneUrl,
                plan.WebUrl,
                plan.Visibility,
                plan.IsArchived,
                plan.ProjectId,
                importedAtUtc,
                plan.OrchestrationStatus,
                plan.OrchestrationStatusReason);

            dbContext.Repositories.Add(repository);
            return (repository, CreatedRepository: true);
        }

        repository.AssignToProject(plan.ProjectId);
        repository.RefreshMetadata(
            plan.DefaultBranch,
            plan.CloneUrl,
            plan.WebUrl,
            plan.Visibility,
            plan.IsArchived,
            importedAtUtc);

        if (plan.IsArchived)
        {
            repository.MarkOrchestrationIneligible("Archived repositories cannot be orchestrated.");
        }
        else
        {
            repository.MarkOrchestrationEligible();
        }

        return (repository, CreatedRepository: false);
    }

    private async Task<(SymphonyInstance? Instance, bool CreatedInstance)> CreateInstanceShellAsync(
        Repository repository,
        RepositoryImportPlan plan,
        DateTimeOffset importedAtUtc,
        CancellationToken cancellationToken)
    {
        if (plan.InstancePlan is null)
        {
            return (null, CreatedInstance: false);
        }

        RepositoryImportInstancePlan instancePlan = plan.InstancePlan;
        SymphonyInstance? existingInstance = await dbContext.SymphonyInstances
            .FirstOrDefaultAsync(
                instance =>
                    instance.RepositoryId == repository.Id &&
                    instance.DisplayName == instancePlan.DisplayName &&
                    instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed,
                cancellationToken);

        if (existingInstance is not null)
        {
            return (existingInstance, CreatedInstance: false);
        }

        SymphonyInstance instance = new(
            SymphonyInstanceId.New(),
            repository.Id,
            instancePlan.DisplayName,
            instancePlan.ExecutionMode,
            instancePlan.BaseUrl,
            importedAtUtc,
            InstanceLifecycleStatus.NotProvisioned,
            InstanceHealthStatus.Unknown,
            port: instancePlan.Port,
            symphonyReleaseTag: instancePlan.ReleaseSelector.ToString(),
            gitHubCredentialSecretId: instancePlan.GitHubCredential.SecretId,
            gitHubCredentialInheritanceMode: instancePlan.GitHubCredential.InheritanceMode,
            openAiCredentialSecretId: instancePlan.OpenAiCredential.SecretId,
            openAiCredentialInheritanceMode: instancePlan.OpenAiCredential.InheritanceMode,
            workflowPath: instancePlan.WorkflowPath,
            dataPath: instancePlan.DataPath,
            workflowProfileId: instancePlan.WorkflowProfileId);

        dbContext.SymphonyInstances.Add(instance);

        return (instance, CreatedInstance: true);
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string fieldName,
        string message)
    {
        if (!errors.TryGetValue(fieldName, out List<string>? messages))
        {
            messages = [];
            errors[fieldName] = messages;
        }

        messages.Add(message);
    }

    private static IReadOnlyDictionary<string, string[]> ToErrorDictionary(
        Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(
            item => item.Key,
            item => item.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private sealed record RepositoryImportReferenceValidationResult(string? ProjectName);
}
