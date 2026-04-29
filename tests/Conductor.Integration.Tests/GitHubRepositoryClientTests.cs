using System.Net;
using System.Text;
using System.Text.Json;
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
        Assert.Contains("application/vnd.github+json", request.Accept, StringComparison.Ordinal);
        Assert.Contains("Conductor/1.0", request.UserAgent, StringComparison.Ordinal);
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
    public async Task ListOrganizationsAsync_Maps_Accessible_Organizations()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            """
            {
              "data": {
                "viewer": {
                  "organizations": {
                    "nodes": [
                      {
                        "login": "ReleasedGroup",
                        "name": "Released Group",
                        "description": "Delivery tooling",
                        "url": "https://github.com/ReleasedGroup",
                        "avatarUrl": "https://avatars.githubusercontent.com/u/42?v=4"
                      }
                    ],
                    "pageInfo": {
                      "hasNextPage": false,
                      "endCursor": null
                    }
                  }
                }
              }
            }
            """));
        GitHubRepositoryClient client = CreateClient(handler);

        IReadOnlyList<GitHubOrganizationSummary> organizations =
            await client.ListOrganizationsAsync(CancellationToken.None);

        GitHubOrganizationSummary organization = Assert.Single(organizations);
        Assert.Equal("ReleasedGroup", organization.Login);
        Assert.Equal("Released Group", organization.DisplayName);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup"), organization.WebUrl);
        Assert.Equal(new Uri("https://avatars.githubusercontent.com/u/42?v=4"), organization.AvatarUrl);
        Assert.Equal("Delivery tooling", organization.Description);
        Assert.Equal("POST /graphql", Format(Assert.Single(handler.Requests)));
        Assert.Contains("organizations", handler.Requests[0].Content ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListRepositoriesAsync_Maps_Core_Metadata()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            """
            {
              "data": {
                "viewer": {
                  "repositories": {
                    "nodes": [
                      {
                        "owner": {
                          "login": "ReleasedGroup"
                        },
                        "name": "TheConductor",
                        "url": "https://github.com/ReleasedGroup/TheConductor",
                        "defaultBranchRef": {
                          "name": "main",
                          "target": {
                            "statusCheckRollup": {
                              "state": "SUCCESS"
                            }
                          }
                        },
                        "visibility": "PRIVATE",
                        "isArchived": false,
                        "issues": {
                          "totalCount": 3
                        },
                        "pullRequests": {
                          "totalCount": 2
                        },
                        "labels": {
                          "nodes": [
                            {
                              "name": "bug",
                              "color": "d73a4a",
                              "description": "Something is not working"
                            }
                          ]
                        },
                        "milestones": {
                          "nodes": [
                            {
                              "title": "Sprint 5",
                              "state": "OPEN",
                              "dueOn": "2026-05-15"
                            }
                          ]
                        },
                        "branchProtectionRules": {
                          "totalCount": 1,
                          "nodes": [
                            {
                              "pattern": "main",
                              "requiresApprovingReviews": true,
                              "requiredApprovingReviewCount": 2,
                              "requiresStatusChecks": true,
                              "requiredStatusCheckContexts": [
                                "build",
                                "test"
                              ]
                            }
                          ]
                        }
                      }
                    ],
                    "pageInfo": {
                      "hasNextPage": false,
                      "endCursor": null
                    }
                  }
                }
              }
            }
            """));
        GitHubRepositoryClient client = CreateClient(handler);

        IReadOnlyList<GitHubRepositorySummary> repositories =
            await client.ListRepositoriesAsync(CancellationToken.None);

        GitHubRepositorySummary repository = Assert.Single(repositories);
        Assert.Equal("ReleasedGroup", repository.Owner);
        Assert.Equal("TheConductor", repository.Name);
        Assert.Equal("ReleasedGroup/TheConductor", repository.FullName);
        Assert.Equal("main", repository.DefaultBranch);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor.git"), repository.CloneUrl);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor"), repository.WebUrl);
        Assert.Equal(RepositoryVisibility.Private, repository.Visibility);
        Assert.False(repository.IsArchived);
        Assert.Equal(3, repository.OpenIssueCount);
        Assert.Equal(2, repository.OpenPullRequestCount);
        Assert.Equal("bug", Assert.Single(repository.Labels).Name);
        Assert.Equal("Sprint 5", Assert.Single(repository.Milestones).Title);
        Assert.Equal(new DateOnly(2026, 5, 15), repository.Milestones[0].DueOn);
        Assert.True(repository.BranchProtection.IsKnown);
        Assert.True(repository.BranchProtection.DefaultBranchProtected);
        Assert.True(repository.BranchProtection.RequiresPullRequestReviews);
        Assert.Equal(2, repository.BranchProtection.RequiredApprovingReviewCount);
        Assert.True(repository.BranchProtection.RequiresStatusChecks);
        Assert.Equal(["build", "test"], repository.BranchProtection.RequiredStatusCheckContexts);
        Assert.True(repository.ActionsStatus.IsKnown);
        Assert.Equal("SUCCESS", repository.ActionsStatus.DefaultBranchStatusCheckState);
    }

    [Fact]
    public async Task SearchRepositoriesAsync_Sends_Query_And_Maps_Results()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            """
            {
              "data": {
                "search": {
                  "nodes": [
                    {
                      "owner": {
                        "login": "ReleasedGroup"
                      },
                      "name": "ArchivedTool",
                      "url": "https://github.com/ReleasedGroup/ArchivedTool",
                      "defaultBranchRef": null,
                      "visibility": "PUBLIC",
                      "isArchived": true,
                      "issues": {
                        "totalCount": 0
                      },
                      "pullRequests": {
                        "totalCount": 0
                      },
                      "labels": {
                        "nodes": []
                      },
                      "milestones": {
                        "nodes": []
                      },
                      "branchProtectionRules": {
                        "totalCount": 0,
                        "nodes": []
                      }
                    }
                  ],
                  "pageInfo": {
                    "hasNextPage": false,
                    "endCursor": null
                  }
                }
              }
            }
            """));
        GitHubRepositoryClient client = CreateClient(handler);

        IReadOnlyList<GitHubRepositorySummary> repositories =
            await client.SearchRepositoriesAsync("org:ReleasedGroup conductor", CancellationToken.None);

        GitHubRepositorySummary repository = Assert.Single(repositories);
        Assert.Equal("ArchivedTool", repository.Name);
        Assert.Equal(string.Empty, repository.DefaultBranch);
        Assert.True(repository.IsArchived);
        Assert.Equal(RepositoryVisibility.Public, repository.Visibility);
        Assert.Equal(GitHubBranchProtectionSummary.None, repository.BranchProtection);

        using JsonDocument requestPayload = JsonDocument.Parse(handler.Requests[0].Content!);
        Assert.Equal(
            "org:ReleasedGroup conductor",
            requestPayload.RootElement.GetProperty("variables").GetProperty("query").GetString());
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

    private static string Format(RecordedRequest request) =>
        $"{request.Method} {request.Uri.AbsolutePath}";

    private static HttpResponseMessage JsonResponse(string json) =>
        JsonResponse(HttpStatusCode.OK, json);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static async Task<RecordedRequest> RecordRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? content = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new RecordedRequest(
            request.Method.Method,
            request.RequestUri ?? throw new InvalidOperationException("Request URI was not set."),
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            string.Join(" ", request.Headers.UserAgent.Select(value => value.ToString())),
            string.Join(", ", request.Headers.Accept.Select(value => value.MediaType)),
            content);
    }

    private sealed class QueueHandler(
        params Func<RecordedRequest, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<RecordedRequest, HttpResponseMessage>> responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RecordedRequest recordedRequest = await RecordRequestAsync(request, cancellationToken);

            Requests.Add(recordedRequest);
            return responses.Dequeue()(recordedRequest);
        }
    }

    private sealed record RecordedRequest(
        string Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string UserAgent,
        string Accept,
        string? Content);
}
