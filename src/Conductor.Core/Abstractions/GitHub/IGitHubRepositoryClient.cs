using Conductor.Core.Domain;
using Conductor.Core.Domain.Repositories;

namespace Conductor.Core.Abstractions.GitHub;

public interface IGitHubRepositoryClient
{
    Task<IReadOnlyList<GitHubOrganizationSummary>> ListOrganizationsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubRepositorySummary>> ListRepositoriesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubRepositorySummary>> SearchRepositoriesAsync(
        string query,
        CancellationToken cancellationToken);

    Task<GitHubRepositoryAccessValidationResult> ValidateRepositoryAccessAsync(
        GitHubRepositoryAccessValidationRequest request,
        CancellationToken cancellationToken);
}

public sealed record GitHubOrganizationSummary(
    string Login,
    string? DisplayName,
    Uri WebUrl,
    Uri? AvatarUrl,
    string? Description);

public sealed record GitHubRepositorySummary
{
    public GitHubRepositorySummary(
        string owner,
        string name,
        string defaultBranch,
        Uri cloneUrl,
        Uri webUrl,
        bool isArchived,
        RepositoryVisibility visibility = RepositoryVisibility.Public,
        int openIssueCount = 0,
        int openPullRequestCount = 0,
        IReadOnlyList<GitHubLabelSummary>? labels = null,
        IReadOnlyList<GitHubMilestoneSummary>? milestones = null,
        GitHubBranchProtectionSummary? branchProtection = null,
        GitHubActionsStatusSummary? actionsStatus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cloneUrl);
        ArgumentNullException.ThrowIfNull(webUrl);

        if (!cloneUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Clone URL must be absolute.", nameof(cloneUrl));
        }

        if (!webUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Web URL must be absolute.", nameof(webUrl));
        }

        Owner = owner.Trim();
        Name = name.Trim();
        DefaultBranch = defaultBranch.Trim();
        CloneUrl = cloneUrl;
        WebUrl = webUrl;
        IsArchived = isArchived;
        Visibility = visibility;
        OpenIssueCount = Math.Max(0, openIssueCount);
        OpenPullRequestCount = Math.Max(0, openPullRequestCount);
        Labels = labels ?? [];
        Milestones = milestones ?? [];
        BranchProtection = branchProtection ?? GitHubBranchProtectionSummary.Unknown;
        ActionsStatus = actionsStatus ?? GitHubActionsStatusSummary.Unknown;
    }

    public GitHubRepositorySummary(
        string owner,
        string name,
        string defaultBranch,
        Uri cloneUrl,
        Uri webUrl,
        RepositoryVisibility visibility,
        bool IsArchived)
        : this(
            owner,
            name,
            defaultBranch,
            cloneUrl,
            webUrl,
            IsArchived,
            visibility)
    {
    }

    public string Owner { get; }

    public string Name { get; }

    public string FullName => $"{Owner}/{Name}";

    public string DefaultBranch { get; }

    public Uri CloneUrl { get; }

    public Uri WebUrl { get; }

    public bool IsArchived { get; }

    public RepositoryVisibility Visibility { get; }

    public int OpenIssueCount { get; }

    public int OpenPullRequestCount { get; }

    public IReadOnlyList<GitHubLabelSummary> Labels { get; }

    public IReadOnlyList<GitHubMilestoneSummary> Milestones { get; }

    public GitHubBranchProtectionSummary BranchProtection { get; }

    public GitHubActionsStatusSummary ActionsStatus { get; }
}

public sealed record GitHubLabelSummary(
    string Name,
    string? Color,
    string? Description);

public sealed record GitHubMilestoneSummary(
    string Title,
    string State,
    DateOnly? DueOn);

public sealed record GitHubBranchProtectionSummary(
    bool IsKnown,
    bool DefaultBranchProtected,
    int ProtectedBranchRuleCount,
    bool RequiresPullRequestReviews,
    int RequiredApprovingReviewCount,
    bool RequiresStatusChecks,
    IReadOnlyList<string> RequiredStatusCheckContexts)
{
    public static GitHubBranchProtectionSummary Unknown { get; } = new(
        IsKnown: false,
        DefaultBranchProtected: false,
        ProtectedBranchRuleCount: 0,
        RequiresPullRequestReviews: false,
        RequiredApprovingReviewCount: 0,
        RequiresStatusChecks: false,
        RequiredStatusCheckContexts: []);

    public static GitHubBranchProtectionSummary None { get; } = new(
        IsKnown: true,
        DefaultBranchProtected: false,
        ProtectedBranchRuleCount: 0,
        RequiresPullRequestReviews: false,
        RequiredApprovingReviewCount: 0,
        RequiresStatusChecks: false,
        RequiredStatusCheckContexts: []);
}

public sealed record GitHubActionsStatusSummary(
    bool IsKnown,
    string? DefaultBranchStatusCheckState)
{
    public static GitHubActionsStatusSummary Unknown { get; } = new(
        IsKnown: false,
        DefaultBranchStatusCheckState: null);
}

public sealed record GitHubRepositoryAccessValidationRequest(
    GitHubRepositoryFullName RepositoryFullName,
    string PersonalAccessToken)
{
    public override string ToString() =>
        $"{nameof(GitHubRepositoryAccessValidationRequest)} {{ {nameof(RepositoryFullName)} = {RepositoryFullName}, {nameof(PersonalAccessToken)} = ******** }}";
}

public sealed record GitHubRepositoryAccessValidationResult(
    GitHubRepositoryAccessValidationStatus Status,
    string Message,
    GitHubRepositoryPermissionSet? Permissions,
    IReadOnlyList<string> TokenScopes,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitResetUtc)
{
    public bool HasRepositoryAccess => Status is GitHubRepositoryAccessValidationStatus.Accessible;
}

public enum GitHubRepositoryAccessValidationStatus
{
    Accessible,
    InvalidToken,
    RepositoryNotAccessible,
    InsufficientRepositoryPermission,
    RateLimited,
    GitHubUnavailable,
}

public sealed record GitHubRepositoryPermissionSet(
    bool Admin,
    bool Maintain,
    bool Push,
    bool Triage,
    bool Pull)
{
    public bool HasReadAccess => Admin || Maintain || Push || Triage || Pull;
}
