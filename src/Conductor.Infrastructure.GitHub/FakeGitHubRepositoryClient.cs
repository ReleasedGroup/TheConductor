using Conductor.Core.Abstractions.GitHub;

namespace Conductor.Infrastructure.GitHub;

public sealed class FakeGitHubRepositoryClient : IGitHubRepositoryClient
{
    private readonly object syncRoot = new();
    private readonly List<GitHubRepositorySummary> repositories = [];
    private readonly List<string> searchQueries = [];
    private readonly List<GitHubRepositoryAccessValidationRequest> validationRequests = [];
    private readonly Queue<Exception> searchFailures = [];

    public FakeGitHubRepositoryClient()
    {
    }

    public FakeGitHubRepositoryClient(IEnumerable<GitHubRepositorySummary> repositories)
    {
        AddRepositories(repositories);
    }

    public IReadOnlyList<GitHubRepositorySummary> Repositories
    {
        get
        {
            lock (syncRoot)
            {
                return repositories.ToArray();
            }
        }
    }

    public IReadOnlyList<string> SearchQueries
    {
        get
        {
            lock (syncRoot)
            {
                return searchQueries.ToArray();
            }
        }
    }

    public IReadOnlyList<GitHubRepositoryAccessValidationRequest> ValidationRequests
    {
        get
        {
            lock (syncRoot)
            {
                return validationRequests.ToArray();
            }
        }
    }

    public void AddRepository(GitHubRepositorySummary repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        lock (syncRoot)
        {
            repositories.Add(repository);
        }
    }

    public void AddRepositories(IEnumerable<GitHubRepositorySummary> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        foreach (GitHubRepositorySummary repository in repositories)
        {
            AddRepository(repository);
        }
    }

    public void QueueSearchFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (syncRoot)
        {
            searchFailures.Enqueue(exception);
        }
    }

    public Task<IReadOnlyList<GitHubRepositorySummary>> SearchRepositoriesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IReadOnlyList<GitHubRepositorySummary>>(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromException<IReadOnlyList<GitHubRepositorySummary>>(
                new ArgumentException("A repository search query is required.", nameof(query)));
        }

        string normalizedQuery = query.Trim();

        lock (syncRoot)
        {
            searchQueries.Add(normalizedQuery);

            if (searchFailures.Count > 0)
            {
                return Task.FromException<IReadOnlyList<GitHubRepositorySummary>>(searchFailures.Dequeue());
            }

            GitHubRepositorySummary[] matches = repositories
                .Where(repository => Matches(repository, normalizedQuery))
                .ToArray();

            return Task.FromResult<IReadOnlyList<GitHubRepositorySummary>>(matches);
        }
    }

    public Task<GitHubRepositoryAccessValidationResult> ValidateRepositoryAccessAsync(
        GitHubRepositoryAccessValidationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<GitHubRepositoryAccessValidationResult>(cancellationToken);
        }

        lock (syncRoot)
        {
            validationRequests.Add(request);

            if (string.IsNullOrWhiteSpace(request.PersonalAccessToken))
            {
                return Task.FromResult(new GitHubRepositoryAccessValidationResult(
                    GitHubRepositoryAccessValidationStatus.InvalidToken,
                    "The selected PAT is empty.",
                    Permissions: null,
                    TokenScopes: [],
                    RateLimitRemaining: null,
                    RateLimitResetUtc: null));
            }

            bool repositoryExists = repositories.Any(repository =>
                string.Equals(repository.Owner, request.RepositoryFullName.Owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(repository.Name, request.RepositoryFullName.Name, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(repositoryExists
                ? new GitHubRepositoryAccessValidationResult(
                    GitHubRepositoryAccessValidationStatus.Accessible,
                    "Fake GitHub confirmed the selected PAT can access the target repository.",
                    new GitHubRepositoryPermissionSet(
                        Admin: false,
                        Maintain: false,
                        Push: false,
                        Triage: true,
                        Pull: true),
                    TokenScopes: ["repo"],
                    RateLimitRemaining: null,
                    RateLimitResetUtc: null)
                : new GitHubRepositoryAccessValidationResult(
                    GitHubRepositoryAccessValidationStatus.RepositoryNotAccessible,
                    "Fake GitHub did not return the target repository.",
                    Permissions: null,
                    TokenScopes: [],
                    RateLimitRemaining: null,
                    RateLimitResetUtc: null));
        }
    }

    private static bool Matches(GitHubRepositorySummary repository, string query) =>
        Contains(repository.Owner, query)
        || Contains(repository.Name, query)
        || Contains(repository.FullName, query);

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
