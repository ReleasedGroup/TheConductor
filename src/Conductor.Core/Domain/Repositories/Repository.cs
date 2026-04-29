using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Repositories;

public sealed class Repository
{
    public Repository(
        RepositoryId id,
        RepositoryProvider provider,
        string owner,
        string name,
        string defaultBranch,
        Uri cloneUrl,
        Uri webUrl,
        RepositoryVisibility visibility,
        bool isArchived,
        ProjectId? projectId,
        DateTimeOffset? lastSyncedAtUtc,
        RepositoryOrchestrationStatus orchestrationStatus,
        string? orchestrationStatusReason)
    {
        Id = new RepositoryId(Guard.NotEmpty(id.Value, nameof(id)));
        Provider = provider;
        Owner = Guard.NotWhiteSpace(owner, nameof(owner));
        Name = Guard.NotWhiteSpace(name, nameof(name));
        DefaultBranch = Guard.NotWhiteSpace(defaultBranch, nameof(defaultBranch));
        CloneUrl = Guard.AbsoluteUri(cloneUrl, nameof(cloneUrl));
        WebUrl = Guard.AbsoluteUri(webUrl, nameof(webUrl));
        Visibility = visibility;
        IsArchived = isArchived;
        ProjectId = projectId;
        LastSyncedAtUtc = lastSyncedAtUtc is null ? null : Guard.Utc(lastSyncedAtUtc.Value, nameof(lastSyncedAtUtc));
        OrchestrationStatus = orchestrationStatus;
        OrchestrationStatusReason = Guard.OptionalTrimmed(orchestrationStatusReason);

        ValidateOrchestrationStatus();
    }

    public RepositoryId Id { get; }

    public RepositoryProvider Provider { get; }

    public string Owner { get; }

    public string Name { get; }

    public string FullName => $"{Owner}/{Name}";

    public string DefaultBranch { get; private set; }

    public Uri CloneUrl { get; private set; }

    public Uri WebUrl { get; private set; }

    public RepositoryVisibility Visibility { get; private set; }

    public bool IsArchived { get; private set; }

    public ProjectId? ProjectId { get; private set; }

    public DateTimeOffset? LastSyncedAtUtc { get; private set; }

    public RepositoryOrchestrationStatus OrchestrationStatus { get; private set; }

    public string? OrchestrationStatusReason { get; private set; }

    public bool IsOrchestrationEligible => OrchestrationStatus == RepositoryOrchestrationStatus.Eligible;

    public void AssignToProject(ProjectId? projectId)
    {
        ProjectId = projectId is null ? null : new ProjectId(Guard.NotEmpty(projectId.Value.Value, nameof(projectId)));
    }

    public void RefreshMetadata(
        string defaultBranch,
        Uri cloneUrl,
        Uri webUrl,
        RepositoryVisibility visibility,
        bool isArchived,
        DateTimeOffset syncedAtUtc)
    {
        var validatedDefaultBranch = Guard.NotWhiteSpace(defaultBranch, nameof(defaultBranch));
        var validatedCloneUrl = Guard.AbsoluteUri(cloneUrl, nameof(cloneUrl));
        var validatedWebUrl = Guard.AbsoluteUri(webUrl, nameof(webUrl));
        var validatedSyncedAtUtc = Guard.Utc(syncedAtUtc, nameof(syncedAtUtc));

        DefaultBranch = validatedDefaultBranch;
        CloneUrl = validatedCloneUrl;
        WebUrl = validatedWebUrl;
        Visibility = visibility;
        IsArchived = isArchived;
        LastSyncedAtUtc = validatedSyncedAtUtc;

        if (isArchived && IsOrchestrationEligible)
        {
            MarkOrchestrationIneligible("Archived repositories cannot be orchestrated.");
        }
    }

    public void MarkOrchestrationEligible()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived repositories cannot be orchestration eligible.");
        }

        OrchestrationStatus = RepositoryOrchestrationStatus.Eligible;
        OrchestrationStatusReason = null;
    }

    public void MarkOrchestrationIneligible(string reason)
    {
        var validatedReason = Guard.NotWhiteSpace(reason, nameof(reason));

        OrchestrationStatus = RepositoryOrchestrationStatus.Ineligible;
        OrchestrationStatusReason = validatedReason;
    }

    private void ValidateOrchestrationStatus()
    {
        if (IsArchived && IsOrchestrationEligible)
        {
            throw new ArgumentException("Archived repositories cannot be orchestration eligible.", nameof(IsArchived));
        }

        if (OrchestrationStatus == RepositoryOrchestrationStatus.Ineligible && OrchestrationStatusReason is null)
        {
            throw new ArgumentException("Ineligible repositories require a status reason.", nameof(OrchestrationStatusReason));
        }

        if (OrchestrationStatus == RepositoryOrchestrationStatus.Eligible && OrchestrationStatusReason is not null)
        {
            throw new ArgumentException("Eligible repositories cannot have an ineligibility reason.", nameof(OrchestrationStatusReason));
        }
    }
}
