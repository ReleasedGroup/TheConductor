using System.Text.Json;
using Conductor.Core.Application.Workflows;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Workflows;

internal sealed class SqliteWorkflowProfileService : IWorkflowProfileService
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 500;

    private readonly ConductorDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public SqliteWorkflowProfileService(
        ConductorDbContext dbContext,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<WorkflowProfileSummary>> ListAsync(
        WorkflowProfileQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<WorkflowProfile> profiles = dbContext.WorkflowProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            profiles = profiles.Where(profile =>
                profile.Name.Contains(search) ||
                (profile.Description != null && profile.Description.Contains(search)));
        }

        return await profiles
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.Name)
            .Select(profile => new WorkflowProfileSummary(
                profile.Id,
                profile.Name,
                profile.Description,
                profile.IsDefault,
                profile.Revision,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<WorkflowProfileDetail?> GetAsync(
        WorkflowProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == profileId)
            .Select(profile => new WorkflowProfileDetail(
                profile.Id,
                profile.Name,
                profile.Description,
                profile.WorkflowSource,
                profile.IsDefault,
                profile.Revision,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowProfileMutationResult> CreateAsync(
        WorkflowProfileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatedWorkflowProfileRequest validated = await ValidateRequestAsync(
            request,
            excludedProfileId: null,
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (validated.IsDefault)
        {
            await ClearDefaultProfilesAsync(excludedProfileId: null, now, cancellationToken);
        }

        WorkflowProfile profile = new(
            WorkflowProfileId.New(),
            validated.Name,
            validated.Description,
            validated.WorkflowSource,
            validated.IsDefault,
            now,
            now);

        dbContext.WorkflowProfiles.Add(profile);
        RecordAuditEvent(
            request.RequestedByUserId,
            "CreateWorkflowProfile",
            profile,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapMutationResult(profile);
    }

    public async Task<WorkflowProfileMutationResult> UpdateAsync(
        WorkflowProfileId profileId,
        WorkflowProfileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatedWorkflowProfileRequest validated = await ValidateRequestAsync(
            request,
            profileId,
            cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        WorkflowProfile profile = await dbContext.WorkflowProfiles
            .FirstOrDefaultAsync(candidate => candidate.Id == profileId, cancellationToken)
            ?? throw new WorkflowProfileNotFoundException(profileId);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (validated.IsDefault)
        {
            await ClearDefaultProfilesAsync(profileId, now, cancellationToken);
        }

        profile.Update(
            validated.Name,
            validated.Description,
            validated.WorkflowSource,
            validated.IsDefault,
            now);

        RecordAuditEvent(
            request.RequestedByUserId,
            "UpdateWorkflowProfile",
            profile,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapMutationResult(profile);
    }

    private async Task<ValidatedWorkflowProfileRequest> ValidateRequestAsync(
        WorkflowProfileMutationRequest request,
        WorkflowProfileId? excludedProfileId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, List<string>> errors = new(StringComparer.Ordinal);
        string name = ValidateRequiredText(
            request.Name,
            nameof(request.Name),
            MaxNameLength,
            "A workflow profile name is required.",
            errors);
        string? description = ValidateOptionalText(
            request.Description,
            nameof(request.Description),
            MaxDescriptionLength,
            errors);
        string workflowSource = ValidateRequiredText(
            request.WorkflowSource,
            nameof(request.WorkflowSource),
            maxLength: null,
            "Workflow source Markdown is required.",
            errors);

        if (errors.Count == 0)
        {
            IReadOnlyList<WorkflowProfileSummary> existingProfiles = await ListAsync(
                new WorkflowProfileQuery(),
                cancellationToken);

            if (existingProfiles.Any(profile =>
                    (!excludedProfileId.HasValue || profile.Id != excludedProfileId.Value) &&
                    string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                AddError(errors, nameof(request.Name), "A workflow profile with this name already exists.");
            }
        }

        if (errors.Count > 0)
        {
            throw new WorkflowProfileValidationException(ToErrorDictionary(errors));
        }

        return new ValidatedWorkflowProfileRequest(
            name,
            description,
            workflowSource,
            request.IsDefault);
    }

    private static string ValidateRequiredText(
        string value,
        string fieldName,
        int? maxLength,
        string requiredMessage,
        Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, fieldName, requiredMessage);
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (maxLength is { } length && trimmed.Length > length)
        {
            AddError(errors, fieldName, $"Enter {length} characters or fewer.");
        }

        return trimmed;
    }

    private static string? ValidateOptionalText(
        string? value,
        string fieldName,
        int maxLength,
        Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            AddError(errors, fieldName, $"Enter {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private async Task ClearDefaultProfilesAsync(
        WorkflowProfileId? excludedProfileId,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<WorkflowProfile> defaultProfileQuery = dbContext.WorkflowProfiles
            .Where(profile => profile.IsDefault);

        if (excludedProfileId.HasValue)
        {
            WorkflowProfileId profileId = excludedProfileId.Value;
            defaultProfileQuery = defaultProfileQuery.Where(profile => profile.Id != profileId);
        }

        WorkflowProfile[] defaultProfiles = await defaultProfileQuery.ToArrayAsync(cancellationToken);

        foreach (WorkflowProfile defaultProfile in defaultProfiles)
        {
            defaultProfile.ClearDefault(updatedAtUtc);
        }
    }

    private void RecordAuditEvent(
        string? requestedByUserId,
        string action,
        WorkflowProfile profile,
        DateTimeOffset occurredAtUtc)
    {
        string metadataJson = JsonSerializer.Serialize(new
        {
            workflowProfileId = profile.Id.ToString(),
            profile.Name,
            profile.IsDefault,
            profile.Revision,
        });

        dbContext.AuditEvents.Add(new AuditEvent(
            AuditEventId.New(),
            string.IsNullOrWhiteSpace(requestedByUserId) ? "system" : requestedByUserId.Trim(),
            action,
            "WorkflowProfile",
            profile.Id.ToString(),
            occurredAtUtc,
            AuditEventOutcome.Succeeded,
            correlationId: null,
            message: $"{action} {profile.Name}.",
            metadataJson: metadataJson));
    }

    private static WorkflowProfileMutationResult MapMutationResult(WorkflowProfile profile) =>
        new(
            profile.Id,
            profile.Name,
            profile.IsDefault,
            profile.Revision,
            profile.UpdatedAtUtc);

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

    private sealed record ValidatedWorkflowProfileRequest(
        string Name,
        string? Description,
        string WorkflowSource,
        bool IsDefault);
}
