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
        bool isArchived,
        ProjectId? projectId)
    {
        Id = id;
        Provider = provider;
        Owner = Guard.NotWhiteSpace(owner, nameof(owner));
        Name = Guard.NotWhiteSpace(name, nameof(name));
        DefaultBranch = Guard.NotWhiteSpace(defaultBranch, nameof(defaultBranch));
        CloneUrl = Guard.AbsoluteUri(cloneUrl, nameof(cloneUrl));
        WebUrl = Guard.AbsoluteUri(webUrl, nameof(webUrl));
        IsArchived = isArchived;
        ProjectId = projectId;
    }

    public RepositoryId Id { get; }

    public RepositoryProvider Provider { get; }

    public string Owner { get; }

    public string Name { get; }

    public string FullName => $"{Owner}/{Name}";

    public string DefaultBranch { get; }

    public Uri CloneUrl { get; }

    public Uri WebUrl { get; }

    public bool IsArchived { get; }

    public ProjectId? ProjectId { get; }
}
