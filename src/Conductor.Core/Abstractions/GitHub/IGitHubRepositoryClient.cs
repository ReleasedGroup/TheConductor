using Conductor.Core.Domain;
using Conductor.Core.Domain.Repositories;

namespace Conductor.Core.Abstractions.GitHub;

public interface IGitHubRepositoryClient
{
    Task<IReadOnlyList<GitHubRepositorySummary>> SearchRepositoriesAsync(
        string query,
        CancellationToken cancellationToken);

    Task<GitHubRepositoryAccessValidationResult> ValidateRepositoryAccessAsync(
        GitHubRepositoryAccessValidationRequest request,
        CancellationToken cancellationToken);
}

public sealed record GitHubRepositorySummary(
    string Owner,
    string Name,
    string DefaultBranch,
    Uri CloneUrl,
    Uri WebUrl,
    RepositoryVisibility Visibility,
    bool IsArchived)
{
    public string FullName => $"{Owner}/{Name}";
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
