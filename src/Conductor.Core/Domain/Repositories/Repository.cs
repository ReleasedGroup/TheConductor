using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Repositories;

public sealed class Repository
{
    public Repository(
        RepositoryId id,
        RepositoryProvider provider,
        GitHubRepositoryFullName fullName,
        string defaultBranch,
        Uri cloneUrl,
        Uri webUrl,
        bool isArchived,
        ProjectId? projectId)
    {
        Id = id;
        Provider = provider;
        FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
        DefaultBranch = Guard.NotWhiteSpace(defaultBranch, nameof(defaultBranch));
        CloneUrl = Guard.AbsoluteUri(cloneUrl, nameof(cloneUrl));
        WebUrl = Guard.AbsoluteUri(webUrl, nameof(webUrl));
        IsArchived = isArchived;
        ProjectId = projectId;
    }

    public Repository(
        RepositoryId id,
        RepositoryProvider provider,
        string owner,
        string name,
        string defaultBranch,
        Uri cloneUrl,
        Uri webUrl,
        bool isArchived,
        ProjectId? projectId)
        : this(
            id,
            provider,
            new GitHubRepositoryFullName(owner, name),
            defaultBranch,
            cloneUrl,
            webUrl,
            isArchived,
            projectId)
    {
    }

    public RepositoryId Id { get; }

    public RepositoryProvider Provider { get; }

    public string Owner => FullName.Owner;

    public string Name => FullName.Name;

    public GitHubRepositoryFullName FullName { get; }

    public string DefaultBranch { get; }

    public Uri CloneUrl { get; }

    public Uri WebUrl { get; }

    public bool IsArchived { get; }

    public ProjectId? ProjectId { get; }
}
