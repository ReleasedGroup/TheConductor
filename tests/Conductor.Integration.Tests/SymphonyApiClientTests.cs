using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Symphony;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Integration.Tests;

public sealed class SymphonyApiClientTests
{
    private static readonly Uri BaseUri = new("http://symphony.local/root/");

    [Fact]
    public async Task Client_Calls_Read_Endpoints_And_Preserves_Raw_Json()
    {
        string healthJson = Fixture("health-ok.json");
        string runtimeJson = Fixture("runtime-basic.json");
        string stateJson = Fixture("state-running.json");
        string issueJson = Fixture("issue-detail.json");
        string refreshJson = """{"accepted":true}""";
        var handler = new QueueHandler(
            _ => JsonResponse(HttpStatusCode.OK, healthJson),
            _ => JsonResponse(HttpStatusCode.OK, runtimeJson),
            _ => JsonResponse(HttpStatusCode.OK, stateJson),
            _ => JsonResponse(HttpStatusCode.OK, issueJson),
            _ => JsonResponse(HttpStatusCode.Accepted, refreshJson));
        using HttpClient httpClient = new(handler);
        var client = new SymphonyApiClient(httpClient);

        SymphonyHealthResponse health = await client.GetHealthAsync(BaseUri, CancellationToken.None);
        SymphonyRuntimeResponse runtime = await client.GetRuntimeAsync(BaseUri, CancellationToken.None);
        SymphonyStateResponse state = await client.GetStateAsync(BaseUri, CancellationToken.None);
        SymphonyIssueResponse? issue = await client.GetIssueAsync(BaseUri, "issue-42", CancellationToken.None);
        SymphonyRefreshResponse refresh = await client.RequestRefreshAsync(BaseUri, CancellationToken.None);

        Assert.Equal(InstanceHealthStatus.Healthy, health.Status);
        Assert.Equal(200, health.HttpStatusCode);
        Assert.Equal(healthJson, health.RawJson);
        Assert.Equal(runtimeJson, runtime.RawJson);
        Assert.Equal(stateJson, state.RawJson);
        Assert.NotNull(issue);
        Assert.Equal("issue-42", issue.IssueIdentifier);
        Assert.Equal(issueJson, issue.RawJson);
        Assert.True(refresh.Accepted);
        Assert.Equal(refreshJson, refresh.RawJson);
        Assert.Equal(
            [
                "GET /root/api/v1/health",
                "GET /root/api/v1/runtime",
                "GET /root/api/v1/state",
                "GET /root/api/v1/issue-42",
                "POST /root/api/v1/refresh",
            ],
            handler.Requests.Select(request => $"{request.Method} {request.Uri.AbsolutePath}"));
    }

    [Fact]
    public async Task Workflow_Methods_Read_And_Save_Source_With_ETags()
    {
        var handler = new QueueHandler(
            _ => JsonResponse(
                HttpStatusCode.OK,
                """{"source":"# Workflow\nsteps: []"}""",
                "\"workflow-1\""),
            _ => EmptyResponse(HttpStatusCode.NoContent, "\"workflow-2\""));
        using HttpClient httpClient = new(handler);
        var client = new SymphonyApiClient(httpClient);

        SymphonyWorkflowDocument workflow = await client.GetWorkflowAsync(BaseUri, CancellationToken.None);
        SymphonyWorkflowDocument saved = await client.SaveWorkflowAsync(
            BaseUri,
            workflow with { Source = "# Updated workflow" },
            CancellationToken.None);

        Assert.Equal("# Workflow\nsteps: []", workflow.Source);
        Assert.Equal("\"workflow-1\"", workflow.ETag);
        Assert.Equal("# Updated workflow", saved.Source);
        Assert.Equal("\"workflow-2\"", saved.ETag);
        Assert.Equal("GET /root/api/v1/workflow", Format(handler.Requests[0]));
        Assert.Equal("PUT /root/api/v1/workflow", Format(handler.Requests[1]));
        Assert.Equal("\"workflow-1\"", handler.Requests[1].IfMatch);

        using JsonDocument payload = JsonDocument.Parse(handler.Requests[1].Content!);
        Assert.Equal("# Updated workflow", payload.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Issue_Returns_Null_For_NotFound_Response()
    {
        var handler = new QueueHandler(_ => JsonResponse(HttpStatusCode.NotFound, """{"error":"missing"}"""));
        using HttpClient httpClient = new(handler);
        var client = new SymphonyApiClient(httpClient);

        SymphonyIssueResponse? issue = await client.GetIssueAsync(BaseUri, "issue-404", CancellationToken.None);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Health_Returns_Offline_For_Request_Failure()
    {
        var handler = new QueueHandler(_ => throw new HttpRequestException("connection refused"));
        using HttpClient httpClient = new(handler);
        var client = new SymphonyApiClient(httpClient);

        SymphonyHealthResponse health = await client.GetHealthAsync(BaseUri, CancellationToken.None);

        Assert.Equal(InstanceHealthStatus.Offline, health.Status);
        Assert.Null(health.HttpStatusCode);
        Assert.Contains("request_failed", health.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConductorSymphony_Registers_Api_Client()
    {
        ServiceCollection services = [];

        services.AddConductorSymphony();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<SymphonyApiClient>(provider.GetRequiredService<ISymphonyApiClient>());
    }

    private static string Fixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Symphony", fileName));

    private static string Format(RecordedRequest request) =>
        $"{request.Method} {request.Uri.AbsolutePath}";

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json,
        string? etag = null)
    {
        HttpResponseMessage response = EmptyResponse(statusCode, etag);
        response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return response;
    }

    private static HttpResponseMessage EmptyResponse(HttpStatusCode statusCode, string? etag = null)
    {
        HttpResponseMessage response = new(statusCode);

        if (etag is not null)
        {
            response.Headers.ETag = new EntityTagHeaderValue(etag);
        }

        return response;
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
            string? content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            string? ifMatch = request.Headers.TryGetValues("If-Match", out IEnumerable<string>? values)
                ? values.SingleOrDefault()
                : null;
            var recordedRequest = new RecordedRequest(
                request.Method.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI was not set."),
                content,
                ifMatch);

            Requests.Add(recordedRequest);

            return responses.Dequeue()(recordedRequest);
        }
    }

    private sealed record RecordedRequest(string Method, Uri Uri, string? Content, string? IfMatch);
}
