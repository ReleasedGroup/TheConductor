using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Repositories;
using Conductor.Infrastructure.GitHub;

namespace Conductor.Core.Tests;

public sealed class FakeGitHubRepositoryClientTests
{
    [Fact]
    public async Task SearchRepositoriesAsync_Filters_Seeded_Repositories_Deterministically()
    {
        var client = new FakeGitHubRepositoryClient(
            [
                Repository("ReleasedGroup", "TheConductor"),
                Repository("ReleasedGroup", "api-service"),
                Repository("OtherOrg", "portal"),
            ]);

        IReadOnlyList<GitHubRepositorySummary> repositories =
            await client.SearchRepositoriesAsync(" releasedgroup/ ", CancellationToken.None);

        Assert.Equal(
            ["ReleasedGroup/TheConductor", "ReleasedGroup/api-service"],
            repositories.Select(repository => repository.FullName));
        Assert.Equal(["releasedgroup/"], client.SearchQueries);
    }

    [Fact]
    public async Task SearchRepositoriesAsync_Uses_Case_Insensitive_Owner_And_Name_Matching()
    {
        var client = new FakeGitHubRepositoryClient();
        client.AddRepository(Repository("ReleasedGroup", "TheConductor"));
        client.AddRepository(Repository("ReleasedGroup", "mobile-shell"));

        IReadOnlyList<GitHubRepositorySummary> repositories =
            await client.SearchRepositoriesAsync("CONDUCTOR", CancellationToken.None);

        GitHubRepositorySummary repository = Assert.Single(repositories);
        Assert.Equal("ReleasedGroup/TheConductor", repository.FullName);
    }

    [Fact]
    public async Task SearchRepositoriesAsync_Returns_Queued_Failure_Then_Resumes()
    {
        var client = new FakeGitHubRepositoryClient([Repository("ReleasedGroup", "TheConductor")]);
        client.QueueSearchFailure(new InvalidOperationException("rate limit"));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SearchRepositoriesAsync("conductor", CancellationToken.None));
        IReadOnlyList<GitHubRepositorySummary> repositories =
            await client.SearchRepositoriesAsync("conductor", CancellationToken.None);

        Assert.Equal("rate limit", exception.Message);
        Assert.Single(repositories);
        Assert.Equal(["conductor", "conductor"], client.SearchQueries);
    }

    [Fact]
    public async Task SearchRepositoriesAsync_Honors_Caller_Cancellation()
    {
        var client = new FakeGitHubRepositoryClient([Repository("ReleasedGroup", "TheConductor")]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SearchRepositoriesAsync("conductor", cancellation.Token));

        Assert.Empty(client.SearchQueries);
    }

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Returns_Access_For_Seeded_Repository()
    {
        var client = new FakeGitHubRepositoryClient([Repository("ReleasedGroup", "TheConductor")]);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(
                new GitHubRepositoryFullName("ReleasedGroup", "TheConductor"),
                "ghp_selected_pat"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.Accessible, result.Status);
        Assert.True(result.HasRepositoryAccess);
        Assert.NotNull(result.Permissions);
        Assert.True(result.Permissions.Pull);
        GitHubRepositoryAccessValidationRequest request = Assert.Single(client.ValidationRequests);
        Assert.DoesNotContain("ghp_selected_pat", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Returns_NotAccessible_For_Unknown_Repository()
    {
        var client = new FakeGitHubRepositoryClient([Repository("ReleasedGroup", "TheConductor")]);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(
                new GitHubRepositoryFullName("ReleasedGroup", "missing"),
                "ghp_selected_pat"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.RepositoryNotAccessible, result.Status);
        Assert.False(result.HasRepositoryAccess);
    }

    private static GitHubRepositorySummary Repository(string owner, string name) =>
        new(
            owner,
            name,
            "main",
            new Uri($"https://github.com/{owner}/{name}.git"),
            new Uri($"https://github.com/{owner}/{name}"),
            RepositoryVisibility.Public,
            IsArchived: false);
}
