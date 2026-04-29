using Conductor.Core.Domain;

namespace Conductor.Core.Abstractions.Symphony;

public interface ISymphonyApiClient
{
    Task<SymphonyHealthResponse> GetHealthAsync(Uri baseUri, CancellationToken cancellationToken);

    Task<SymphonyRuntimeResponse> GetRuntimeAsync(Uri baseUri, CancellationToken cancellationToken);

    Task<SymphonyWorkflowDocument> GetWorkflowAsync(Uri baseUri, CancellationToken cancellationToken);

    Task<SymphonyWorkflowDocument> SaveWorkflowAsync(
        Uri baseUri,
        SymphonyWorkflowDocument document,
        CancellationToken cancellationToken);

    Task<SymphonyStateResponse> GetStateAsync(Uri baseUri, CancellationToken cancellationToken);

    Task<SymphonyIssueResponse?> GetIssueAsync(
        Uri baseUri,
        string issueIdentifier,
        CancellationToken cancellationToken);

    Task<SymphonyRefreshResponse> RequestRefreshAsync(Uri baseUri, CancellationToken cancellationToken);
}

public sealed record SymphonyHealthResponse(
    InstanceHealthStatus Status,
    int? HttpStatusCode,
    TimeSpan Latency,
    string RawJson);

public sealed record SymphonyRuntimeResponse(string RawJson);

public sealed record SymphonyWorkflowDocument(string Source, string? ETag);

public sealed record SymphonyStateResponse(string RawJson);

public sealed record SymphonyIssueResponse(string IssueIdentifier, string RawJson);

public sealed record SymphonyRefreshResponse(bool Accepted, string RawJson);
