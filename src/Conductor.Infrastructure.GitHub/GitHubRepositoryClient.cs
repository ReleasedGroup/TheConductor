using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Domain;

namespace Conductor.Infrastructure.GitHub;

public sealed class GitHubRepositoryClient(HttpClient httpClient) : IGitHubRepositoryClient
{
    public const string HttpClientName = "GitHubRepository";

    private const int PageSize = 100;

    private static readonly Uri DefaultApiBaseUri = new("https://api.github.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ProductInfoHeaderValue UserAgent = new("Conductor", "1.0");

    public async Task<IReadOnlyList<GitHubOrganizationSummary>> ListOrganizationsAsync(
        CancellationToken cancellationToken)
    {
        List<GitHubOrganizationSummary> organizations = [];
        string? after = null;

        do
        {
            using JsonDocument document = await SendGraphQlAsync(
                ViewerOrganizationsQuery,
                new { after },
                cancellationToken);

            JsonElement connection = document.RootElement
                .GetProperty("data")
                .GetProperty("viewer")
                .GetProperty("organizations");

            foreach (JsonElement node in ReadNodes(connection))
            {
                organizations.Add(ToOrganizationSummary(node));
            }

            after = ReadNextCursor(connection);
        }
        while (after is not null);

        return organizations;
    }

    public async Task<IReadOnlyList<GitHubRepositorySummary>> ListRepositoriesAsync(
        CancellationToken cancellationToken)
    {
        List<GitHubRepositorySummary> repositories = [];
        string? after = null;

        do
        {
            using JsonDocument document = await SendGraphQlAsync(
                ViewerRepositoriesQuery,
                new { after },
                cancellationToken);

            JsonElement connection = document.RootElement
                .GetProperty("data")
                .GetProperty("viewer")
                .GetProperty("repositories");

            foreach (JsonElement node in ReadNodes(connection))
            {
                repositories.Add(ToRepositorySummary(node));
            }

            after = ReadNextCursor(connection);
        }
        while (after is not null);

        return repositories;
    }

    public async Task<IReadOnlyList<GitHubRepositorySummary>> SearchRepositoriesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = RequireNonEmpty(query, nameof(query));
        List<GitHubRepositorySummary> repositories = [];
        string? after = null;

        do
        {
            using JsonDocument document = await SendGraphQlAsync(
                SearchRepositoriesQuery,
                new { query = normalizedQuery, after },
                cancellationToken);

            JsonElement connection = document.RootElement
                .GetProperty("data")
                .GetProperty("search");

            foreach (JsonElement node in ReadNodes(connection))
            {
                repositories.Add(ToRepositorySummary(node));
            }

            after = ReadNextCursor(connection);
        }
        while (after is not null);

        return repositories;
    }

    public async Task<GitHubRepositoryAccessValidationResult> ValidateRepositoryAccessAsync(
        GitHubRepositoryAccessValidationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string token = RequireNonEmpty(request.PersonalAccessToken, nameof(request.PersonalAccessToken));
        string endpoint =
            $"repos/{Uri.EscapeDataString(request.RepositoryFullName.Owner)}/{Uri.EscapeDataString(request.RepositoryFullName.Name)}";

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

    private async Task<JsonDocument> SendGraphQlAsync(
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "graphql", personalAccessToken: null);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new GraphQlRequest(query, variables), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        string body = await ReadBodyAsync(response, cancellationToken);
        EnsureGraphQlSuccess(response, body);

        JsonDocument document = JsonDocument.Parse(body);
        EnsureNoGraphQlErrors(document);

        return document;
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

    private static void EnsureGraphQlSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string suffix = TryReadGitHubMessage(body) is { Length: > 0 } message
            ? $" {message}"
            : string.Empty;

        throw new HttpRequestException(
            $"GitHub GraphQL endpoint returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).{suffix}",
            inner: null,
            response.StatusCode);
    }

    private static void EnsureNoGraphQlErrors(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("errors", out JsonElement errors)
            || errors.ValueKind != JsonValueKind.Array
            || errors.GetArrayLength() == 0)
        {
            return;
        }

        string[] messages = errors
            .EnumerateArray()
            .Select(error => ReadOptionalString(error, "message"))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message!)
            .ToArray();

        string joinedMessages = messages.Length == 0
            ? "Unknown GraphQL error."
            : string.Join("; ", messages);

        throw new HttpRequestException($"GitHub GraphQL returned errors: {joinedMessages}");
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

        return reset is null
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(reset.Value);
    }

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

    private static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("GitHub returned an empty JSON response.");
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException("GitHub returned an unexpected JSON response.");
    }

    private static GitHubOrganizationSummary ToOrganizationSummary(JsonElement node)
    {
        string login = ReadRequiredString(node, "login");

        return new GitHubOrganizationSummary(
            login,
            ReadOptionalString(node, "name"),
            ReadRequiredUri(node, "url"),
            ReadOptionalUri(node, "avatarUrl"),
            ReadOptionalString(node, "description"));
    }

    private static GitHubRepositorySummary ToRepositorySummary(JsonElement node)
    {
        string owner = ReadRequiredString(node.GetProperty("owner"), "login");
        string name = ReadRequiredString(node, "name");
        string defaultBranch = ReadDefaultBranch(node);

        return new GitHubRepositorySummary(
            owner,
            name,
            defaultBranch,
            BuildHttpsCloneUrl(owner, name),
            ReadRequiredUri(node, "url"),
            ReadBoolean(node, "isArchived"),
            MapVisibility(ReadOptionalString(node, "visibility")),
            ReadConnectionTotalCount(node, "issues"),
            ReadConnectionTotalCount(node, "pullRequests"),
            ReadLabels(node),
            ReadMilestones(node),
            ReadBranchProtection(node, defaultBranch),
            ReadActionsStatus(node));
    }

    private static IReadOnlyList<JsonElement> ReadNodes(JsonElement connection)
    {
        if (!connection.TryGetProperty("nodes", out JsonElement nodes)
            || nodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return nodes
            .EnumerateArray()
            .Where(node => node.ValueKind == JsonValueKind.Object)
            .ToArray();
    }

    private static string? ReadNextCursor(JsonElement connection)
    {
        if (!connection.TryGetProperty("pageInfo", out JsonElement pageInfo)
            || !ReadBoolean(pageInfo, "hasNextPage"))
        {
            return null;
        }

        return ReadOptionalString(pageInfo, "endCursor");
    }

    private static string ReadDefaultBranch(JsonElement repository)
    {
        if (repository.TryGetProperty("defaultBranchRef", out JsonElement branch)
            && branch.ValueKind == JsonValueKind.Object)
        {
            return ReadOptionalString(branch, "name")?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static IReadOnlyList<GitHubLabelSummary> ReadLabels(JsonElement repository)
    {
        if (!repository.TryGetProperty("labels", out JsonElement labelsConnection))
        {
            return [];
        }

        return ReadNodes(labelsConnection)
            .Select(label => new GitHubLabelSummary(
                ReadRequiredString(label, "name"),
                ReadOptionalString(label, "color"),
                ReadOptionalString(label, "description")))
            .ToArray();
    }

    private static IReadOnlyList<GitHubMilestoneSummary> ReadMilestones(JsonElement repository)
    {
        if (!repository.TryGetProperty("milestones", out JsonElement milestonesConnection))
        {
            return [];
        }

        return ReadNodes(milestonesConnection)
            .Select(milestone => new GitHubMilestoneSummary(
                ReadRequiredString(milestone, "title"),
                ReadOptionalString(milestone, "state") ?? "OPEN",
                ReadDateOnly(milestone, "dueOn")))
            .ToArray();
    }

    private static GitHubBranchProtectionSummary ReadBranchProtection(
        JsonElement repository,
        string defaultBranch)
    {
        if (!repository.TryGetProperty("branchProtectionRules", out JsonElement rulesConnection))
        {
            return GitHubBranchProtectionSummary.Unknown;
        }

        int totalCount = ReadInteger(rulesConnection, "totalCount");
        if (totalCount == 0)
        {
            return GitHubBranchProtectionSummary.None;
        }

        bool defaultBranchProtected = false;
        bool requiresReviews = false;
        bool requiresStatusChecks = false;
        int requiredApprovals = 0;
        HashSet<string> contexts = new(StringComparer.Ordinal);

        foreach (JsonElement rule in ReadNodes(rulesConnection))
        {
            string pattern = ReadOptionalString(rule, "pattern") ?? string.Empty;
            defaultBranchProtected |= RuleCanMatchBranch(pattern, defaultBranch);
            requiresReviews |= ReadBoolean(rule, "requiresApprovingReviews");
            requiresStatusChecks |= ReadBoolean(rule, "requiresStatusChecks");
            requiredApprovals = Math.Max(
                requiredApprovals,
                ReadInteger(rule, "requiredApprovingReviewCount"));

            if (rule.TryGetProperty("requiredStatusCheckContexts", out JsonElement checkContexts)
                && checkContexts.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement context in checkContexts.EnumerateArray())
                {
                    if (context.ValueKind == JsonValueKind.String
                        && context.GetString() is { Length: > 0 } value)
                    {
                        contexts.Add(value);
                    }
                }
            }
        }

        return new GitHubBranchProtectionSummary(
            IsKnown: true,
            defaultBranchProtected,
            totalCount,
            requiresReviews,
            requiredApprovals,
            requiresStatusChecks,
            contexts.Order(StringComparer.Ordinal).ToArray());
    }

    private static GitHubActionsStatusSummary ReadActionsStatus(JsonElement repository)
    {
        if (!repository.TryGetProperty("defaultBranchRef", out JsonElement branch)
            || branch.ValueKind != JsonValueKind.Object
            || !branch.TryGetProperty("target", out JsonElement target)
            || target.ValueKind != JsonValueKind.Object
            || !target.TryGetProperty("statusCheckRollup", out JsonElement statusCheckRollup)
            || statusCheckRollup.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new GitHubActionsStatusSummary(
                IsKnown: true,
                DefaultBranchStatusCheckState: null);
        }

        return new GitHubActionsStatusSummary(
            IsKnown: true,
            ReadOptionalString(statusCheckRollup, "state"));
    }

    private static bool RuleCanMatchBranch(string pattern, string branch)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(branch))
        {
            return false;
        }

        if (string.Equals(pattern, branch, StringComparison.Ordinal))
        {
            return true;
        }

        if (pattern == "*")
        {
            return true;
        }

        if (pattern.EndsWith('*') && branch.StartsWith(pattern[..^1], StringComparison.Ordinal))
        {
            return true;
        }

        return pattern.StartsWith('*') && branch.EndsWith(pattern[1..], StringComparison.Ordinal);
    }

    private static RepositoryVisibility MapVisibility(string? visibility) =>
        visibility?.ToUpperInvariant() switch
        {
            "PRIVATE" => RepositoryVisibility.Private,
            "INTERNAL" => RepositoryVisibility.Internal,
            _ => RepositoryVisibility.Public,
        };

    private static int ReadConnectionTotalCount(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement connection)
            || connection.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return ReadInteger(connection, "totalCount");
    }

    private static int ReadInteger(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value)
            ? value
            : 0;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.True
            || (property.ValueKind == JsonValueKind.String
                && bool.TryParse(property.GetString(), out bool value)
                && value);
    }

    private static Uri BuildHttpsCloneUrl(string owner, string name) =>
        new($"https://github.com/{owner}/{name}.git");

    private static Uri ReadRequiredUri(JsonElement element, string propertyName)
    {
        string value = ReadRequiredString(element, propertyName);

        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new InvalidOperationException($"GitHub response field '{propertyName}' was not an absolute URI.");
    }

    private static Uri? ReadOptionalUri(JsonElement element, string propertyName)
    {
        string? value = ReadOptionalString(element, propertyName);

        return value is not null && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            ? uri
            : null;
    }

    private static DateOnly? ReadDateOnly(JsonElement element, string propertyName)
    {
        string? value = ReadOptionalString(element, propertyName);

        return value is not null
            && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
                ? date
                : null;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName) =>
        ReadOptionalString(element, propertyName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"GitHub response field '{propertyName}' was missing or empty.");

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind == JsonValueKind.Null
            || property.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static string? TryReadGitHubMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return ReadOptionalString(document.RootElement, "message");
        }
        catch (JsonException)
        {
            return null;
        }
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

    private sealed record GraphQlRequest(string Query, object Variables);

    private const string RepositoryFieldsFragment =
        """
        fragment RepositoryFields on Repository {
          owner {
            login
          }
          name
          url
          defaultBranchRef {
            name
            target {
              ... on Commit {
                statusCheckRollup {
                  state
                }
              }
            }
          }
          visibility
          isArchived
          issues(states: OPEN) {
            totalCount
          }
          pullRequests(states: OPEN) {
            totalCount
          }
          labels(first: 50, orderBy: { field: NAME, direction: ASC }) {
            nodes {
              name
              color
              description
            }
          }
          milestones(first: 50, states: OPEN, orderBy: { field: DUE_DATE, direction: ASC }) {
            nodes {
              title
              state
              dueOn
            }
          }
          branchProtectionRules(first: 20) {
            totalCount
            nodes {
              pattern
              requiresApprovingReviews
              requiredApprovingReviewCount
              requiresStatusChecks
              requiredStatusCheckContexts
            }
          }
        }
        """;

    private static readonly string ViewerOrganizationsQuery =
        $$"""
        query($after: String) {
          viewer {
            organizations(first: {{PageSize}}, after: $after) {
              nodes {
                login
                name
                description
                url
                avatarUrl
              }
              pageInfo {
                hasNextPage
                endCursor
              }
            }
          }
        }
        """;

    private static readonly string ViewerRepositoriesQuery =
        $$"""
        query($after: String) {
          viewer {
            repositories(
              first: {{PageSize}},
              after: $after,
              affiliations: [OWNER, COLLABORATOR, ORGANIZATION_MEMBER],
              orderBy: { field: NAME, direction: ASC }) {
              nodes {
                ...RepositoryFields
              }
              pageInfo {
                hasNextPage
                endCursor
              }
            }
          }
        }

        {{RepositoryFieldsFragment}}
        """;

    private static readonly string SearchRepositoriesQuery =
        $$"""
        query($query: String!, $after: String) {
          search(query: $query, type: REPOSITORY, first: {{PageSize}}, after: $after) {
            nodes {
              ... on Repository {
                ...RepositoryFields
              }
            }
            pageInfo {
              hasNextPage
              endCursor
            }
          }
        }

        {{RepositoryFieldsFragment}}
        """;
}
