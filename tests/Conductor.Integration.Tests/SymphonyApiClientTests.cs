using System.Net;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Symphony;

namespace Conductor.Integration.Tests;

public sealed class SymphonyApiClientTests
{
    [Fact]
    public async Task GetHealthAsync_Calls_Health_Endpoint_And_Maps_Status()
    {
        List<string> requestedPaths = [];
        using HttpClient httpClient = CreateHttpClient(request =>
        {
            requestedPaths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);

            return JsonResponse(HttpStatusCode.OK, ReadFixture("health-ok.json"));
        });
        SymphonyApiClient client = new(httpClient);

        var response = await client.GetHealthAsync(new Uri("http://localhost:5173"), CancellationToken.None);

        Assert.Equal(InstanceHealthStatus.Healthy, response.Status);
        Assert.Equal(200, response.HttpStatusCode);
        Assert.Equal("/api/v1/health", Assert.Single(requestedPaths));
        Assert.Contains("healthy", response.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRuntimeAsync_Returns_Raw_Runtime_Json()
    {
        using HttpClient httpClient = CreateHttpClient(request =>
        {
            Assert.Equal("/ops/api/v1/runtime", request.RequestUri?.AbsolutePath);

            return JsonResponse(HttpStatusCode.OK, ReadFixture("runtime-basic.json"));
        });
        SymphonyApiClient client = new(httpClient);

        var response = await client.GetRuntimeAsync(new Uri("http://localhost:5173/ops/"), CancellationToken.None);

        Assert.Contains("\"version\": \"3.1.4\"", response.RawJson, StringComparison.Ordinal);
        Assert.Contains("\"owner\": \"ReleasedGroup\"", response.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStateAsync_Throws_When_State_Endpoint_Fails()
    {
        using HttpClient httpClient = CreateHttpClient(_ =>
            JsonResponse(HttpStatusCode.ServiceUnavailable, """{"error":"offline"}"""));
        SymphonyApiClient client = new(httpClient);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetStateAsync(new Uri("http://localhost:5173"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new HttpClient(new StubHttpMessageHandler(handler))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Symphony", fileName));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
