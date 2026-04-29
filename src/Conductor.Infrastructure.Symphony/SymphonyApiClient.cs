using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;

namespace Conductor.Infrastructure.Symphony;

public sealed class SymphonyApiClient(HttpClient httpClient) : ISymphonyApiClient
{
    public const string HttpClientName = "SymphonyApi";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WorkflowSaveTimeout = TimeSpan.FromSeconds(15);

    public async Task<SymphonyHealthResponse> GetHealthAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                baseUri,
                "api/v1/health",
                HealthTimeout,
                cancellationToken);

            string rawJson = NormalizeRawJson(await ReadBodyAsync(response, cancellationToken));

            return new SymphonyHealthResponse(
                MapHealthStatus(response, rawJson),
                (int)response.StatusCode,
                stopwatch.Elapsed,
                rawJson);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                null,
                stopwatch.Elapsed,
                JsonSerializer.Serialize(
                    new HealthFailure("timeout", "Symphony health request timed out."),
                    JsonOptions));
        }
        catch (HttpRequestException exception)
        {
            return new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                null,
                stopwatch.Elapsed,
                JsonSerializer.Serialize(
                    new HealthFailure("request_failed", exception.Message),
                    JsonOptions));
        }
    }

    public async Task<SymphonyRuntimeResponse> GetRuntimeAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        const string endpoint = "api/v1/runtime";

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            endpoint,
            ReadTimeout,
            cancellationToken);

        string rawJson = await ReadBodyAsync(response, cancellationToken);
        EnsureSuccess(response, endpoint);

        return new SymphonyRuntimeResponse(rawJson);
    }

    public async Task<SymphonyWorkflowDocument> GetWorkflowAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        const string endpoint = "api/v1/workflow";

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            endpoint,
            ReadTimeout,
            cancellationToken);

        string body = await ReadBodyAsync(response, cancellationToken);
        EnsureSuccess(response, endpoint);

        return ToWorkflowDocument(body, response, fallback: null);
    }

    public async Task<SymphonyWorkflowDocument> SaveWorkflowAsync(
        Uri baseUri,
        SymphonyWorkflowDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        const string endpoint = "api/v1/workflow";
        string payload = JsonSerializer.Serialize(new WorkflowPayload(document.Source), JsonOptions);

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Put,
            baseUri,
            endpoint,
            WorkflowSaveTimeout,
            cancellationToken,
            payload,
            request =>
            {
                if (!string.IsNullOrWhiteSpace(document.ETag))
                {
                    request.Headers.TryAddWithoutValidation("If-Match", document.ETag);
                }
            });

        string body = await ReadBodyAsync(response, cancellationToken);
        EnsureSuccess(response, endpoint);

        return ToWorkflowDocument(body, response, document);
    }

    public async Task<SymphonyStateResponse> GetStateAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        const string endpoint = "api/v1/state";

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            endpoint,
            ReadTimeout,
            cancellationToken);

        string rawJson = await ReadBodyAsync(response, cancellationToken);
        EnsureSuccess(response, endpoint);

        return new SymphonyStateResponse(rawJson);
    }

    public async Task<SymphonyIssueResponse?> GetIssueAsync(
        Uri baseUri,
        string issueIdentifier,
        CancellationToken cancellationToken)
    {
        string normalizedIdentifier = RequireNonEmpty(issueIdentifier, nameof(issueIdentifier));
        string endpoint = $"api/v1/{Uri.EscapeDataString(normalizedIdentifier)}";

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            baseUri,
            endpoint,
            ReadTimeout,
            cancellationToken);

        string rawJson = await ReadBodyAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, endpoint);

        return new SymphonyIssueResponse(normalizedIdentifier, rawJson);
    }

    public async Task<SymphonyRefreshResponse> RequestRefreshAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        const string endpoint = "api/v1/refresh";

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            baseUri,
            endpoint,
            ReadTimeout,
            cancellationToken);

        string rawJson = NormalizeRawJson(await ReadBodyAsync(response, cancellationToken));
        EnsureSuccess(response, endpoint);

        return new SymphonyRefreshResponse(
            response.StatusCode == HttpStatusCode.Accepted || response.IsSuccessStatusCode,
            rawJson);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri baseUri,
        string endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? jsonContent = null,
        Action<HttpRequestMessage>? configureRequest = null)
    {
        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        using HttpRequestMessage request = new(method, BuildEndpointUri(baseUri, endpoint));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (jsonContent is not null)
        {
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        }

        configureRequest?.Invoke(request);

        return await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            timeoutSource.Token);
    }

    private static Uri BuildEndpointUri(Uri baseUri, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Symphony base URI must be absolute.", nameof(baseUri));
        }

        UriBuilder builder = new(baseUri)
        {
            Fragment = string.Empty,
            Query = string.Empty,
        };

        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return new Uri(builder.Uri, endpoint);
    }

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

    private static void EnsureSuccess(HttpResponseMessage response, string endpoint)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"Symphony endpoint '{endpoint}' returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
            inner: null,
            response.StatusCode);
    }

    private static string NormalizeRawJson(string rawJson) =>
        rawJson.Length == 0 ? "{}" : rawJson;

    private static InstanceHealthStatus MapHealthStatus(
        HttpResponseMessage response,
        string rawJson)
    {
        InstanceHealthStatus? reportedStatus = TryReadHealthStatus(rawJson);

        if (reportedStatus.HasValue)
        {
            return reportedStatus.Value;
        }

        if (response.IsSuccessStatusCode)
        {
            return InstanceHealthStatus.Healthy;
        }

        return (int)response.StatusCode >= 500
            ? InstanceHealthStatus.Critical
            : InstanceHealthStatus.Warning;
    }

    private static InstanceHealthStatus? TryReadHealthStatus(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? status = ReadStringProperty(document.RootElement, "status")
                ?? ReadStringProperty(document.RootElement, "healthStatus")
                ?? ReadStringProperty(document.RootElement, "health");

            return status is null ? null : ParseHealthStatus(status);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static InstanceHealthStatus? ParseHealthStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "healthy" or "ok" or "up" => InstanceHealthStatus.Healthy,
            "warning" or "warn" or "degraded" => InstanceHealthStatus.Warning,
            "critical" or "unhealthy" or "failed" => InstanceHealthStatus.Critical,
            "offline" or "down" or "unreachable" => InstanceHealthStatus.Offline,
            "unknown" => InstanceHealthStatus.Unknown,
            _ => Enum.TryParse(status, ignoreCase: true, out InstanceHealthStatus parsed)
                ? parsed
                : null,
        };

    private static SymphonyWorkflowDocument ToWorkflowDocument(
        string body,
        HttpResponseMessage response,
        SymphonyWorkflowDocument? fallback)
    {
        string? etag = response.Headers.ETag?.Tag ?? fallback?.ETag;

        if (body.Length == 0)
        {
            return fallback is null
                ? new SymphonyWorkflowDocument(string.Empty, etag)
                : fallback with { ETag = etag };
        }

        return new SymphonyWorkflowDocument(ExtractWorkflowSource(body), etag);
    }

    private static string ExtractWorkflowSource(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                return document.RootElement.GetString() ?? string.Empty;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return body;
            }

            return ReadStringProperty(document.RootElement, "source")
                ?? ReadStringProperty(document.RootElement, "workflowSource")
                ?? ReadStringProperty(document.RootElement, "content")
                ?? ReadStringProperty(document.RootElement, "workflow")
                ?? body;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName)
                || string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : null;
            }
        }

        return null;
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private sealed record WorkflowPayload(string Source);

    private sealed record HealthFailure(string Kind, string Message);
}
