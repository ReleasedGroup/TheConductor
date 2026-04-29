using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;

namespace Conductor.Infrastructure.Symphony;

public sealed class SymphonyApiClient : ISymphonyApiClient
{
    public const string HttpClientName = "SymphonyApi";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RuntimeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WorkflowTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient httpClient;

    public SymphonyApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<SymphonyHealthResponse> GetHealthAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            "health",
            content: null,
            HealthTimeout,
            cancellationToken);
        string rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        InstanceHealthStatus status = response.IsSuccessStatusCode
            ? MapHealthStatus(rawJson)
            : InstanceHealthStatus.Offline;

        return new SymphonyHealthResponse(
            status,
            (int)response.StatusCode,
            response.RequestMessage?.Options.TryGetValue(RequestLatencyKey, out TimeSpan latency) == true
                ? latency
                : TimeSpan.Zero,
            string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson);
    }

    public async Task<SymphonyRuntimeResponse> GetRuntimeAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            "runtime",
            content: null,
            RuntimeTimeout,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return new SymphonyRuntimeResponse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<SymphonyWorkflowDocument> GetWorkflowAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            "workflow",
            content: null,
            WorkflowTimeout,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        string source = ExtractString(rawContent, "source") ?? rawContent;
        string? etag = response.Headers.ETag?.Tag;

        return new SymphonyWorkflowDocument(source, etag);
    }

    public async Task<SymphonyWorkflowDocument> SaveWorkflowAsync(
        Uri baseUri,
        SymphonyWorkflowDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        using StringContent content = new(
            JsonSerializer.Serialize(new { source = document.Source }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Put,
            baseUri,
            "workflow",
            content,
            WorkflowTimeout,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        string source = ExtractString(rawContent, "source") ?? document.Source;
        string? etag = response.Headers.ETag?.Tag;

        return new SymphonyWorkflowDocument(source, etag);
    }

    public async Task<SymphonyStateResponse> GetStateAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            "state",
            content: null,
            StateTimeout,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return new SymphonyStateResponse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<SymphonyIssueResponse?> GetIssueAsync(
        Uri baseUri,
        string issueIdentifier,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issueIdentifier))
        {
            throw new ArgumentException("An issue identifier is required.", nameof(issueIdentifier));
        }

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            Uri.EscapeDataString(issueIdentifier.Trim()),
            content: null,
            RuntimeTimeout,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return new SymphonyIssueResponse(
            issueIdentifier.Trim(),
            await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<SymphonyRefreshResponse> RequestRefreshAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            baseUri,
            "refresh",
            content: null,
            RuntimeTimeout,
            cancellationToken);
        string rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new SymphonyRefreshResponse(true, string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson);
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return new SymphonyRefreshResponse(true, string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson);
    }

    private static readonly HttpRequestOptionsKey<TimeSpan> RequestLatencyKey = new("SymphonyRequestLatency");

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri baseUri,
        string apiPath,
        HttpContent? content,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        using HttpRequestMessage request = new(method, BuildEndpoint(baseUri, apiPath))
        {
            Content = content,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);
        stopwatch.Stop();
        response.RequestMessage?.Options.Set(RequestLatencyKey, stopwatch.Elapsed);

        return response;
    }

    private static Uri BuildEndpoint(Uri baseUri, string apiPath)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("A Symphony base URI must be absolute.", nameof(baseUri));
        }

        UriBuilder builder = new(baseUri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
        };
        string basePath = builder.Path.TrimEnd('/');
        builder.Path = string.IsNullOrEmpty(basePath) ? "/" : basePath + "/";

        return new Uri(builder.Uri, $"api/v1/{apiPath.TrimStart('/')}");
    }

    private static InstanceHealthStatus MapHealthStatus(string rawJson)
    {
        string? rawStatus = ExtractString(rawJson, "status")
            ?? ExtractString(rawJson, "healthStatus")
            ?? ExtractString(rawJson, "state");

        return rawStatus?.Trim().ToLowerInvariant() switch
        {
            "ok" or "up" or "healthy" or "ready" => InstanceHealthStatus.Healthy,
            "warn" or "warning" or "degraded" => InstanceHealthStatus.Warning,
            "critical" or "unhealthy" or "failed" => InstanceHealthStatus.Critical,
            "offline" or "down" => InstanceHealthStatus.Offline,
            _ => InstanceHealthStatus.Healthy,
        };
    }

    private static string? ExtractString(string rawJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);

            return FindString(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                string? nested = FindString(property.Value, propertyName);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                string? nested = FindString(item, propertyName);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message = string.IsNullOrWhiteSpace(body)
            ? $"Symphony returned HTTP {(int)response.StatusCode}."
            : $"Symphony returned HTTP {(int)response.StatusCode}: {body}";

        throw new HttpRequestException(message, inner: null, response.StatusCode);
    }
}
