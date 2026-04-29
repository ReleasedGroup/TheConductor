using System.Net;
using System.Net.Http.Headers;
using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Repositories;
using Conductor.Infrastructure.GitHub;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Integration.Tests;

public sealed class GitHubRepositoryClientTests
{
    private static readonly GitHubRepositoryFullName Repository = new("ReleasedGroup", "TheConductor");

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Confirms_Target_Repository_Access()
    {
        var handler = new QueueHandler(_ =>
        {
            HttpResponseMessage response = JsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "owner": { "login": "ReleasedGroup" },
                  "name": "TheConductor",
                  "default_branch": "main",
                  "clone_url": "https://github.com/ReleasedGroup/TheConductor.git",
                  "html_url": "https://github.com/ReleasedGroup/TheConductor",
                  "archived": false,
                  "permissions": {
                    "admin": false,
                    "maintain": false,
                    "push": false,
                    "triage": true,
                    "pull": true
                  }
                }
                """);
            response.Headers.Add("X-OAuth-Scopes", "repo, workflow");
            response.Headers.Add("X-RateLimit-Remaining", "4999");
            return response;
        });
        GitHubRepositoryClient client = CreateClient(handler);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(Repository, "ghp_selected_pat"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.Accessible, result.Status);
        Assert.True(result.HasRepositoryAccess);
        Assert.NotNull(result.Permissions);
        Assert.True(result.Permissions.Pull);
        Assert.Equal(["repo", "workflow"], result.TokenScopes);
        Assert.Equal(4999, result.RateLimitRemaining);
        Assert.DoesNotContain("ghp_selected_pat", result.Message, StringComparison.Ordinal);

        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET /repos/ReleasedGroup/TheConductor", $"{request.Method} {request.Uri.AbsolutePath}");
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("ghp_selected_pat", request.AuthorizationParameter);
        Assert.Contains("application/vnd.github+json", request.Accept);
        Assert.Contains("Conductor/1.0", request.UserAgent);
    }

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Returns_InvalidToken_For_Bad_Credentials()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.Unauthorized,
            """{"message":"Bad credentials"}"""));
        GitHubRepositoryClient client = CreateClient(handler);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(Repository, "bad-token"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.InvalidToken, result.Status);
        Assert.False(result.HasRepositoryAccess);
        Assert.Null(result.Permissions);
        Assert.Contains("rejected", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bad-token", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Returns_NotAccessible_For_NotFound()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.NotFound,
            """{"message":"Not Found"}"""));
        GitHubRepositoryClient client = CreateClient(handler);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(Repository, "ghp_selected_pat"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.RepositoryNotAccessible, result.Status);
        Assert.False(result.HasRepositoryAccess);
        Assert.Contains("owner/name", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Returns_RateLimited_When_GitHub_Limits_Token()
    {
        var handler = new QueueHandler(_ =>
        {
            HttpResponseMessage response = JsonResponse(
                HttpStatusCode.Forbidden,
                """{"message":"API rate limit exceeded"}""");
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", "1777406400");
            return response;
        });
        GitHubRepositoryClient client = CreateClient(handler);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(Repository, "ghp_selected_pat"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.RateLimited, result.Status);
        Assert.Equal(0, result.RateLimitRemaining);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1777406400), result.RateLimitResetUtc);
    }

    [Fact]
    public async Task ValidateRepositoryAccessAsync_Reports_Insufficient_Permission_When_Returned()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "owner": { "login": "ReleasedGroup" },
              "name": "TheConductor",
              "default_branch": "main",
              "clone_url": "https://github.com/ReleasedGroup/TheConductor.git",
              "html_url": "https://github.com/ReleasedGroup/TheConductor",
              "archived": false,
              "permissions": {
                "admin": false,
                "maintain": false,
                "push": false,
                "triage": false,
                "pull": false
              }
            }
            """));
        GitHubRepositoryClient client = CreateClient(handler);

        GitHubRepositoryAccessValidationResult result = await client.ValidateRepositoryAccessAsync(
            new GitHubRepositoryAccessValidationRequest(Repository, "ghp_selected_pat"),
            CancellationToken.None);

        Assert.Equal(GitHubRepositoryAccessValidationStatus.InsufficientRepositoryPermission, result.Status);
        Assert.NotNull(result.Permissions);
        Assert.False(result.Permissions.HasReadAccess);
    }

    [Fact]
    public async Task SearchRepositoriesAsync_Maps_Search_Response_Metadata()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "items": [
                {
                  "owner": { "login": "ReleasedGroup" },
                  "name": "TheConductor",
                  "default_branch": "main",
                  "clone_url": "https://github.com/ReleasedGroup/TheConductor.git",
                  "html_url": "https://github.com/ReleasedGroup/TheConductor",
                  "private": true,
                  "visibility": "internal",
                  "archived": false
                }
              ]
            }
            """));
        GitHubRepositoryClient client = CreateClient(handler);

        IReadOnlyList<GitHubRepositorySummary> repositories = await client.SearchRepositoriesAsync(
            "releasedgroup conductor",
            CancellationToken.None);

        GitHubRepositorySummary repository = Assert.Single(repositories);
        Assert.Equal("ReleasedGroup", repository.Owner);
        Assert.Equal("TheConductor", repository.Name);
        Assert.Equal("main", repository.DefaultBranch);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor.git"), repository.CloneUrl);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor"), repository.WebUrl);
        Assert.Equal(RepositoryVisibility.Internal, repository.Visibility);
        Assert.False(repository.IsArchived);

        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET /search/repositories", $"{request.Method} {request.Uri.AbsolutePath}");
        Assert.Equal("?q=releasedgroup%20conductor&per_page=25", request.Uri.Query);
        Assert.Null(request.AuthorizationScheme);
    }

    [Fact]
    public void AddConductorGitHub_Registers_Repository_Client()
    {
        ServiceCollection services = [];

        services.AddConductorGitHub();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<GitHubRepositoryClient>(provider.GetRequiredService<IGitHubRepositoryClient>());
    }

    [Fact]
    public void Validation_Request_ToString_Masks_Token()
    {
        var request = new GitHubRepositoryAccessValidationRequest(Repository, "ghp_selected_pat");

        Assert.DoesNotContain("ghp_selected_pat", request.ToString(), StringComparison.Ordinal);
    }

    private static GitHubRepositoryClient CreateClient(QueueHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.test/"),
        };

        return new GitHubRepositoryClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return response;
    }

    private sealed class QueueHandler(
        params Func<RecordedRequest, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<RecordedRequest, HttpResponseMessage>> responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var recordedRequest = new RecordedRequest(
                request.Method.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI was not set."),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                string.Join(" ", request.Headers.UserAgent.Select(value => value.ToString())),
                string.Join(", ", request.Headers.Accept.Select(value => value.MediaType)));

            Requests.Add(recordedRequest);
            return Task.FromResult(responses.Dequeue()(recordedRequest));
        }
    }

    private sealed record RecordedRequest(
        string Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string UserAgent,
        string Accept);
}
