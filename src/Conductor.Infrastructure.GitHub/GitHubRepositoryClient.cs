using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Domain;

namespace Conductor.Infrastructure.GitHub;

public sealed class GitHubRepositoryClient(HttpClient httpClient) : IGitHubRepositoryClient
{
    public const string HttpClientName = "GitHubRepository";

    private static readonly Uri DefaultApiBaseUri = new("https://api.github.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ProductInfoHeaderValue UserAgent = new("Conductor", "1.0");

    public async Task<IReadOnlyList<GitHubRepositorySummary>> SearchRepositoriesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = RequireNonEmpty(query, nameof(query));
        string endpoint = $"search/repositories?q={Uri.EscapeDataString(normalizedQuery)}&per_page=25";

        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, endpoint, personalAccessToken: null);
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        string body = await ReadBodyAsync(response, cancellationToken);
        EnsureSuccess(response, "GitHub repository search");

        GitHubSearchResponse searchResponse = Deserialize<GitHubSearchResponse>(body);

        return searchResponse.Items
            .Select(item => new GitHubRepositorySummary(
                item.Owner.Login,
                item.Name,
                item.DefaultBranch,
                new Uri(item.CloneUrl),
                new Uri(item.HtmlUrl),
                MapVisibility(item),
                item.Archived))
            .ToList();
    }

    public async Task<GitHubRepositoryAccessValidationResult> ValidateRepositoryAccessAsync(
        GitHubRepositoryAccessValidationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string token = RequireNonEmpty(request.PersonalAccessToken, nameof(request.PersonalAccessToken));
        string endpoint = $"repos/{Uri.EscapeDataString(request.RepositoryFullName.Owner)}/{Uri.EscapeDataString(request.RepositoryFullName.Name)}";

        using HttpRequestMessage httpRequest = CreateRequest(HttpMethod.Get, endpoint, token);
        using HttpResponseMessage response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        string body = await ReadBodyAsync(response, cancellationToken);
        ValidationResponseMetadata metadata = ReadMetadata(response);

        if (response.IsSuccessStatusCode)
        {
            GitHubRepositoryResponse repository = Deserialize<GitHubRepositoryResponse>(body);
            GitHubRepositoryPermissionSet? permissions = ToPermissionSet(repository.Permissions);

            if (permissions is not null && !permissions.HasReadAccess)
            {
                return CreateResult(
                    GitHubRepositoryAccessValidationStatus.InsufficientRepositoryPermission,
                    "GitHub returned the target repository, but the selected PAT does not report repository read permission.",
                    metadata,
                    permissions);
            }

            return CreateResult(
                GitHubRepositoryAccessValidationStatus.Accessible,
                permissions is null
                    ? "GitHub confirmed repository metadata access. Repository-level permission details were not returned."
                    : "GitHub confirmed the selected PAT can access the target repository.",
                metadata,
                permissions);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => CreateResult(
                GitHubRepositoryAccessValidationStatus.InvalidToken,
                "GitHub rejected the selected PAT. Confirm the token is active and was copied correctly.",
                metadata,
                permissions: null),
            HttpStatusCode.NotFound => CreateResult(
                GitHubRepositoryAccessValidationStatus.RepositoryNotAccessible,
                "GitHub did not return the target repository. Confirm the owner/name and selected PAT repository access.",
                metadata,
                permissions: null),
            HttpStatusCode.Forbidden when metadata.RateLimitRemaining == 0 => CreateResult(
                GitHubRepositoryAccessValidationStatus.RateLimited,
                "GitHub rate limited the selected PAT before repository access could be validated.",
                metadata,
                permissions: null),
            HttpStatusCode.Forbidden => CreateResult(
                GitHubRepositoryAccessValidationStatus.RepositoryNotAccessible,
                "GitHub refused the repository validation request. Confirm token authorization, SSO, and repository policy.",
                metadata,
                permissions: null),
            _ => CreateResult(
                GitHubRepositoryAccessValidationStatus.GitHubUnavailable,
                $"GitHub repository validation returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                metadata,
                permissions: null),
        };
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string endpoint,
        string? personalAccessToken)
    {
        var request = new HttpRequestMessage(method, BuildEndpointUri(endpoint));
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrWhiteSpace(personalAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken.Trim());
        }

        return request;
    }

    private Uri BuildEndpointUri(string endpoint)
    {
        Uri baseUri = httpClient.BaseAddress ?? DefaultApiBaseUri;
        return new Uri(baseUri, endpoint);
    }

    private static GitHubRepositoryAccessValidationResult CreateResult(
        GitHubRepositoryAccessValidationStatus status,
        string message,
        ValidationResponseMetadata metadata,
        GitHubRepositoryPermissionSet? permissions) =>
        new(
            status,
            message,
            permissions,
            metadata.TokenScopes,
            metadata.RateLimitRemaining,
            metadata.RateLimitResetUtc);

    private static GitHubRepositoryPermissionSet? ToPermissionSet(GitHubRepositoryPermissions? permissions) =>
        permissions is null
            ? null
            : new GitHubRepositoryPermissionSet(
                permissions.Admin,
                permissions.Maintain,
                permissions.Push,
                permissions.Triage,
                permissions.Pull);

    private static RepositoryVisibility MapVisibility(GitHubRepositoryResponse repository)
    {
        if (Enum.TryParse(repository.Visibility, ignoreCase: true, out RepositoryVisibility visibility) &&
            Enum.IsDefined(visibility))
        {
            return visibility;
        }

        return repository.IsPrivate
            ? RepositoryVisibility.Private
            : RepositoryVisibility.Public;
    }

    private static ValidationResponseMetadata ReadMetadata(HttpResponseMessage response) =>
        new(
            ReadTokenScopes(response),
            ReadIntHeader(response, "X-RateLimit-Remaining"),
            ReadRateLimitReset(response));

    private static IReadOnlyList<string> ReadTokenScopes(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-OAuth-Scopes", out IEnumerable<string>? values))
        {
            return [];
        }

        return values
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static int? ReadIntHeader(HttpResponseMessage response, string headerName)
    {
        if (!response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
        {
            return null;
        }

        string? value = values.FirstOrDefault();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ReadRateLimitReset(HttpResponseMessage response)
    {
        int? reset = ReadIntHeader(response, "X-RateLimit-Reset");

        if (reset is null)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(reset.Value);
    }

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"{operation} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
            inner: null,
            response.StatusCode);
    }

    private static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("GitHub returned an empty JSON response.");
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException("GitHub returned an unexpected JSON response.");
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }

    private sealed record ValidationResponseMetadata(
        IReadOnlyList<string> TokenScopes,
        int? RateLimitRemaining,
        DateTimeOffset? RateLimitResetUtc);

    private sealed record GitHubSearchResponse
    {
        public IReadOnlyList<GitHubRepositoryResponse> Items { get; init; } = [];
    }

    private sealed record GitHubRepositoryResponse
    {
        public GitHubRepositoryOwner Owner { get; init; } = new();

        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("default_branch")]
        public string DefaultBranch { get; init; } = string.Empty;

        [JsonPropertyName("clone_url")]
        public string CloneUrl { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("private")]
        public bool IsPrivate { get; init; }

        public string? Visibility { get; init; }

        public bool Archived { get; init; }

        public GitHubRepositoryPermissions? Permissions { get; init; }
    }

    private sealed record GitHubRepositoryOwner
    {
        public string Login { get; init; } = string.Empty;
    }

    private sealed record GitHubRepositoryPermissions
    {
        public bool Admin { get; init; }

        public bool Maintain { get; init; }

        public bool Push { get; init; }

        public bool Triage { get; init; }

        public bool Pull { get; init; }
    }
}
