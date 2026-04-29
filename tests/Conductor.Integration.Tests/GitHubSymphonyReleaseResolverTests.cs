using System.Net;
using System.Net.Http.Headers;
using Conductor.Core.Abstractions.Releases;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Releases;
using Conductor.Infrastructure.GitHub;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Integration.Tests;

public sealed class GitHubSymphonyReleaseResolverTests
{
    [Fact]
    public async Task ResolveAsync_Uses_Latest_Endpoint_And_Selects_Local_Target_Asset()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v0.0.7-alpha",
              "html_url": "https://github.com/releasedgroup/symphony/releases/tag/v0.0.7-alpha",
              "published_at": "2026-04-29T00:10:00Z",
              "prerelease": true,
              "assets": [
                {
                  "name": "symphony-linux-x64.tar.gz",
                  "browser_download_url": "https://github.com/releasedgroup/symphony/releases/download/v0.0.7-alpha/symphony-linux-x64.tar.gz",
                  "content_type": "application/gzip",
                  "size": 1200,
                  "digest": "sha256:linux"
                },
                {
                  "name": "symphony-win-x64.zip",
                  "browser_download_url": "https://github.com/releasedgroup/symphony/releases/download/v0.0.7-alpha/symphony-win-x64.zip",
                  "content_type": "application/zip",
                  "size": 1250,
                  "digest": "sha256:windows"
                }
              ]
            }
            """));
        GitHubSymphonyReleaseResolver resolver = CreateResolver(handler);

        ResolvedSymphonyRelease release = await resolver.ResolveAsync(
            ReleaseSelector.Latest,
            new RuntimeTarget(ExecutionMode.LocalProcess, "windows", "x64"),
            CancellationToken.None);

        Assert.Equal("v0.0.7-alpha", release.ReleaseTag.Value);
        Assert.Equal(ReleaseSelector.Latest, release.RequestedSelector);
        Assert.True(release.IsPrerelease);
        Assert.Equal(DateTimeOffset.Parse("2026-04-29T00:10:00Z"), release.PublishedAtUtc);
        Assert.Equal("symphony-win-x64.zip", release.SelectedAsset.Name);
        Assert.Equal(SymphonyReleaseAssetKind.ReleaseArchive, release.SelectedAsset.Kind);
        Assert.Equal("sha256:windows", release.SelectedAsset.Checksum);
        Assert.Equal(2, release.Assets.Count);

        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET /repos/releasedgroup/symphony/releases/latest", $"{request.Method} {request.Uri.AbsolutePath}");
        Assert.Contains("application/vnd.github+json", request.Accept);
        Assert.Contains("Conductor/1.0", request.UserAgent);
    }

    [Fact]
    public async Task ResolveAsync_Uses_Tag_Endpoint_For_Pinned_Releases()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v0.0.6-alpha",
              "html_url": "https://github.com/releasedgroup/symphony/releases/tag/v0.0.6-alpha",
              "published_at": "2026-04-28T03:00:00Z",
              "prerelease": true,
              "assets": [
                {
                  "name": "symphony-linux-x64.tar.gz",
                  "browser_download_url": "https://github.com/releasedgroup/symphony/releases/download/v0.0.6-alpha/symphony-linux-x64.tar.gz",
                  "content_type": "application/gzip",
                  "size": 1000,
                  "digest": "sha256:pinned"
                }
              ]
            }
            """));
        GitHubSymphonyReleaseResolver resolver = CreateResolver(handler);

        ResolvedSymphonyRelease release = await resolver.ResolveAsync(
            ReleaseSelector.PinnedTag("v0.0.6-alpha"),
            new RuntimeTarget(ExecutionMode.LocalProcess, "linux", "x64"),
            CancellationToken.None);

        Assert.Equal("v0.0.6-alpha", release.ReleaseTag.Value);
        Assert.Equal("symphony-linux-x64.tar.gz", release.SelectedAsset.Name);

        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET /repos/releasedgroup/symphony/releases/tags/v0.0.6-alpha", $"{request.Method} {request.Uri.AbsolutePath}");
    }

    [Fact]
    public async Task ResolveAsync_Prefers_Container_Image_Metadata_For_Docker_Target()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v0.0.7-alpha",
              "html_url": "https://github.com/releasedgroup/symphony/releases/tag/v0.0.7-alpha",
              "published_at": "2026-04-29T00:10:00Z",
              "prerelease": true,
              "assets": [
                {
                  "name": "symphony-linux-x64.tar.gz",
                  "browser_download_url": "https://github.com/releasedgroup/symphony/releases/download/v0.0.7-alpha/symphony-linux-x64.tar.gz",
                  "content_type": "application/gzip",
                  "size": 1200,
                  "digest": "sha256:linux"
                },
                {
                  "name": "symphony-container-linux-x64.json",
                  "browser_download_url": "https://github.com/releasedgroup/symphony/releases/download/v0.0.7-alpha/symphony-container-linux-x64.json",
                  "content_type": "application/json",
                  "size": 500,
                  "digest": "sha256:image"
                }
              ]
            }
            """));
        GitHubSymphonyReleaseResolver resolver = CreateResolver(handler);

        ResolvedSymphonyRelease release = await resolver.ResolveAsync(
            ReleaseSelector.Latest,
            new RuntimeTarget(ExecutionMode.Docker, "linux", "x64"),
            CancellationToken.None);

        Assert.Equal("symphony-container-linux-x64.json", release.SelectedAsset.Name);
        Assert.Equal(SymphonyReleaseAssetKind.ContainerImageMetadata, release.SelectedAsset.Kind);
    }

    [Fact]
    public async Task ResolveAsync_Fails_When_No_Compatible_Asset_Is_Published()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v0.0.7-alpha",
              "html_url": "https://github.com/releasedgroup/symphony/releases/tag/v0.0.7-alpha",
              "published_at": "2026-04-29T00:10:00Z",
              "prerelease": true,
              "assets": [
                {
                  "name": "symphony-linux-x64.tar.gz",
                  "browser_download_url": "https://github.com/releasedgroup/symphony/releases/download/v0.0.7-alpha/symphony-linux-x64.tar.gz",
                  "content_type": "application/gzip",
                  "size": 1200,
                  "digest": "sha256:linux"
                }
              ]
            }
            """));
        GitHubSymphonyReleaseResolver resolver = CreateResolver(handler);

        SymphonyReleaseResolutionException exception = await Assert.ThrowsAsync<SymphonyReleaseResolutionException>(() =>
            resolver.ResolveAsync(
                ReleaseSelector.Latest,
                new RuntimeTarget(ExecutionMode.LocalProcess, "windows", "arm64"),
                CancellationToken.None));

        Assert.Contains("No Symphony release asset compatible with LocalProcess windows/arm64", exception.Message, StringComparison.Ordinal);
        Assert.Contains("symphony-linux-x64.tar.gz", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_Reports_Missing_Pinned_Tag()
    {
        var handler = new QueueHandler(_ => JsonResponse(
            HttpStatusCode.NotFound,
            """{"message":"Not Found"}"""));
        GitHubSymphonyReleaseResolver resolver = CreateResolver(handler);

        SymphonyReleaseResolutionException exception = await Assert.ThrowsAsync<SymphonyReleaseResolutionException>(() =>
            resolver.ResolveAsync(
                ReleaseSelector.PinnedTag("v9.9.9"),
                new RuntimeTarget(ExecutionMode.LocalProcess, "linux", "x64"),
                CancellationToken.None));

        Assert.Contains("v9.9.9", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddConductorGitHub_Registers_Symphony_Release_Resolver()
    {
        ServiceCollection services = [];

        services.AddConductorGitHub();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<GitHubSymphonyReleaseResolver>(provider.GetRequiredService<ISymphonyReleaseResolver>());
    }

    private static GitHubSymphonyReleaseResolver CreateResolver(QueueHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.test/"),
        };

        return new GitHubSymphonyReleaseResolver(httpClient);
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
                string.Join(" ", request.Headers.UserAgent.Select(value => value.ToString())),
                string.Join(", ", request.Headers.Accept.Select(value => value.MediaType)));

            Requests.Add(recordedRequest);
            return Task.FromResult(responses.Dequeue()(recordedRequest));
        }
    }

    private sealed record RecordedRequest(
        string Method,
        Uri Uri,
        string UserAgent,
        string Accept);
}
