using System.Text.Json;
using Conductor.Core.Application.Instances;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using DomainEvent = Conductor.Core.Domain.Events.Event;

namespace Conductor.Infrastructure.Persistence.Sqlite.Instances;

internal sealed class SqliteInstanceCredentialAssignmentService : IInstanceCredentialAssignmentService
{
    private static readonly SecretType[] OpenAiCredentialTypes =
    [
        SecretType.OpenAiApiKey,
        SecretType.CodexHome,
    ];

    private readonly ConductorDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public SqliteInstanceCredentialAssignmentService(
        ConductorDbContext dbContext,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<InstanceCredentialAssignmentResult> AssignAsync(
        InstanceCredentialAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);
        ValidateSelectionShape("gitHubCredential", request.GitHubCredential, errors);
        ValidateSelectionShape("openAiCredential", request.OpenAiCredential, errors);
        ThrowIfValidationFailed(errors);

        SymphonyInstance? instance = await dbContext.SymphonyInstances
            .FirstOrDefaultAsync(candidate => candidate.Id == request.InstanceId, cancellationToken);

        if (instance is null)
        {
            throw new SymphonyInstanceNotFoundException(request.InstanceId.ToString());
        }

        Repository repository = await dbContext.Repositories
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == instance.RepositoryId, cancellationToken);

        SecretDescriptor? gitHubDescriptor = await ResolveSpecificDescriptorAsync(
            "gitHubCredential",
            request.GitHubCredential,
            [SecretType.GitHubToken],
            instance,
            repository,
            errors,
            cancellationToken);
        SecretDescriptor? openAiDescriptor = await ResolveSpecificDescriptorAsync(
            "openAiCredential",
            request.OpenAiCredential,
            OpenAiCredentialTypes,
            instance,
            repository,
            errors,
            cancellationToken);
        ThrowIfValidationFailed(errors);

        instance.ConfigureCredentials(
            request.GitHubCredential.SecretId,
            request.GitHubCredential.InheritanceMode,
            request.OpenAiCredential.SecretId,
            request.OpenAiCredential.InheritanceMode);

        DateTimeOffset now = timeProvider.GetUtcNow();
        string metadataJson = JsonSerializer.Serialize(new
        {
            instanceId = instance.Id.ToString(),
            repositoryId = instance.RepositoryId.ToString(),
            gitHubCredential = DescribeAssignment(request.GitHubCredential, gitHubDescriptor),
            openAiCredential = DescribeAssignment(request.OpenAiCredential, openAiDescriptor),
        });

        dbContext.Events.Add(new DomainEvent(
            EventId.New(),
            instance.Id,
            instance.RepositoryId,
            issueNumber: null,
            EventSeverity.Information,
            "SymphonyInstanceCredentialsAssigned",
            $"Updated credential assignment for Symphony instance {instance.DisplayName}.",
            metadataJson,
            now));
        dbContext.AuditEvents.Add(new AuditEvent(
            AuditEventId.New(),
            string.IsNullOrWhiteSpace(request.RequestedByUserId) ? "system" : request.RequestedByUserId.Trim(),
            "AssignSymphonyInstanceCredentials",
            "SymphonyInstance",
            instance.Id.ToString(),
            now,
            AuditEventOutcome.Succeeded,
            correlationId: null,
            message: $"Updated credential assignment for Symphony instance {instance.DisplayName}.",
            metadataJson: metadataJson));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InstanceCredentialAssignmentResult(
            instance.Id.ToString(),
            ToSummary(request.GitHubCredential, gitHubDescriptor),
            ToSummary(request.OpenAiCredential, openAiDescriptor),
            now);
    }

    private static void ValidateSelectionShape(
        string fieldName,
        CredentialAssignmentSelection selection,
        Dictionary<string, List<string>> errors)
    {
        if (!Enum.IsDefined(selection.InheritanceMode))
        {
            AddError(errors, $"{fieldName}.inheritanceMode", "Credential inheritance mode is not supported.");
            return;
        }

        if (selection.InheritanceMode == CredentialInheritanceMode.SpecificSecret)
        {
            if (selection.SecretId is null)
            {
                AddError(errors, $"{fieldName}.secretId", "Select a secret when using a specific credential.");
            }

            return;
        }

        if (selection.SecretId is not null)
        {
            AddError(errors, $"{fieldName}.secretId", "Secret ids can only be supplied when using a specific credential.");
        }
    }

    private async Task<SecretDescriptor?> ResolveSpecificDescriptorAsync(
        string fieldName,
        CredentialAssignmentSelection selection,
        IReadOnlyCollection<SecretType> allowedTypes,
        SymphonyInstance instance,
        Repository repository,
        Dictionary<string, List<string>> errors,
        CancellationToken cancellationToken)
    {
        if (selection.InheritanceMode != CredentialInheritanceMode.SpecificSecret ||
            selection.SecretId is null)
        {
            return null;
        }

        SecretId secretId = selection.SecretId.Value;
        SecretDescriptor? descriptor = await dbContext.SecretDescriptors
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == secretId, cancellationToken);

        if (descriptor is null)
        {
            AddError(errors, $"{fieldName}.secretId", "Selected secret does not exist.");
            return null;
        }

        if (!allowedTypes.Contains(descriptor.SecretType))
        {
            string supportedTypes = string.Join(", ", allowedTypes.Select(type => type.ToString()));
            AddError(errors, $"{fieldName}.secretId", $"Selected secret must have one of these types: {supportedTypes}.");
        }

        if (!IsScopeCompatible(descriptor, instance, repository))
        {
            AddError(errors, $"{fieldName}.secretId", "Selected secret is scoped to a different project, repository, or instance.");
        }

        return descriptor;
    }

    private static bool IsScopeCompatible(
        SecretDescriptor descriptor,
        SymphonyInstance instance,
        Repository repository)
    {
        return descriptor.ScopeType switch
        {
            SecretScopeType.Global => descriptor.ScopeId is null,
            SecretScopeType.Project => repository.ProjectId is not null &&
                string.Equals(descriptor.ScopeId, repository.ProjectId.Value.ToString(), StringComparison.OrdinalIgnoreCase),
            SecretScopeType.Repository => string.Equals(descriptor.ScopeId, repository.Id.ToString(), StringComparison.OrdinalIgnoreCase),
            SecretScopeType.SymphonyInstance => string.Equals(descriptor.ScopeId, instance.Id.ToString(), StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static object DescribeAssignment(
        CredentialAssignmentSelection selection,
        SecretDescriptor? descriptor)
    {
        return new
        {
            inheritanceMode = selection.InheritanceMode.ToString(),
            secretId = selection.SecretId?.ToString(),
            secretName = descriptor?.Name,
            secretType = descriptor?.SecretType.ToString(),
            scopeType = descriptor?.ScopeType.ToString(),
            scopeId = descriptor?.ScopeId,
        };
    }

    private static CredentialAssignmentSummary ToSummary(
        CredentialAssignmentSelection selection,
        SecretDescriptor? descriptor)
    {
        return new CredentialAssignmentSummary(
            selection.InheritanceMode.ToString(),
            selection.SecretId?.ToString(),
            descriptor?.Name,
            descriptor?.SecretType.ToString(),
            descriptor?.ScopeType.ToString(),
            descriptor?.ScopeId);
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string fieldName,
        string message)
    {
        if (!errors.TryGetValue(fieldName, out List<string>? fieldErrors))
        {
            fieldErrors = [];
            errors[fieldName] = fieldErrors;
        }

        fieldErrors.Add(message);
    }

    private static void ThrowIfValidationFailed(Dictionary<string, List<string>> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InstanceCredentialAssignmentValidationException(
            errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.Ordinal));
    }
}
