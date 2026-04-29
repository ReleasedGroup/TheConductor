namespace Conductor.Core.Abstractions.GitHub;

public interface IGitHubRepositoryClient
{
    Task<IReadOnlyList<GitHubRepositorySummary>> SearchRepositoriesAsync(
        string query,
        CancellationToken cancellationToken);
}

public sealed record GitHubRepositorySummary(
    string Owner,
    string Name,
    string DefaultBranch,
    Uri CloneUrl,
    Uri WebUrl,
    bool IsArchived)
{
    public string FullName => $"{Owner}/{Name}";
}
