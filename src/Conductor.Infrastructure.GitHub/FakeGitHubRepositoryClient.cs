using Conductor.Core.Abstractions.GitHub;

namespace Conductor.Infrastructure.GitHub;

public sealed class FakeGitHubRepositoryClient : IGitHubRepositoryClient
{
    private readonly object syncRoot = new();
    private readonly List<GitHubRepositorySummary> repositories = [];
    private readonly List<string> searchQueries = [];
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

    private static bool Matches(GitHubRepositorySummary repository, string query) =>
        Contains(repository.Owner, query)
        || Contains(repository.Name, query)
        || Contains(repository.FullName, query);

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
