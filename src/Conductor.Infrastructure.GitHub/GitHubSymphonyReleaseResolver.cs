using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conductor.Core.Abstractions.Releases;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Releases;

namespace Conductor.Infrastructure.GitHub;

public sealed class GitHubSymphonyReleaseResolver(HttpClient httpClient) : ISymphonyReleaseResolver
{
    public const string HttpClientName = "GitHubSymphonyReleases";

    private const string SymphonyRepositoryOwner = "releasedgroup";
    private const string SymphonyRepositoryName = "symphony";

    private static readonly Uri DefaultApiBaseUri = new("https://api.github.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ProductInfoHeaderValue UserAgent = new("Conductor", "1.0");

    public async Task<ResolvedSymphonyRelease> ResolveAsync(
        ReleaseSelector selector,
        RuntimeTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(target);

        string endpoint = selector.IsLatest
            ? $"repos/{SymphonyRepositoryOwner}/{SymphonyRepositoryName}/releases/latest"
            : $"repos/{SymphonyRepositoryOwner}/{SymphonyRepositoryName}/releases/tags/{Uri.EscapeDataString(selector.Tag!.Value)}";

        using HttpRequestMessage request = CreateRequest(endpoint);
        using HttpResponseMessage response = await SendAsync(request, selector, cancellationToken);
        string body = await ReadBodyAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpFailure(selector, response);
        }

        GitHubReleaseResponse release = Deserialize(body);
        ReleaseTag releaseTag = new(RequireNonEmpty(release.TagName, "GitHub release tag name"));
        Uri releaseUrl = CreateAbsoluteUri(release.HtmlUrl, "GitHub release URL");
        ResolvedSymphonyReleaseAsset[] assets = release.Assets
            .Select(ToResolvedAsset)
            .ToArray();

        if (assets.Length == 0)
        {
            throw new SymphonyReleaseResolutionException(
                $"Symphony release '{releaseTag}' does not publish any downloadable assets.");
        }

        ResolvedSymphonyReleaseAsset selectedAsset = SelectAsset(selector, target, releaseTag, assets);

        return new ResolvedSymphonyRelease(
            selector,
            releaseTag,
            releaseUrl,
            release.PublishedAtUtc,
            release.Prerelease,
            selectedAsset,
            assets);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        ReleaseSelector selector,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new SymphonyReleaseResolutionException(
                $"GitHub Releases request failed while resolving Symphony release selector '{selector}'.",
                ex);
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BuildEndpointUri(endpoint));
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        return request;
    }

    private Uri BuildEndpointUri(string endpoint)
    {
        Uri baseUri = httpClient.BaseAddress ?? DefaultApiBaseUri;
        return new Uri(baseUri, endpoint);
    }

    private static SymphonyReleaseResolutionException CreateHttpFailure(
        ReleaseSelector selector,
        HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return selector.IsLatest
                ? new SymphonyReleaseResolutionException(
                    $"GitHub did not return a latest Symphony release from {SymphonyRepositoryOwner}/{SymphonyRepositoryName}.")
                : new SymphonyReleaseResolutionException(
                    $"Symphony release tag '{selector.Tag}' was not found in {SymphonyRepositoryOwner}/{SymphonyRepositoryName}.");
        }

        return new SymphonyReleaseResolutionException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"GitHub Releases returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) while resolving Symphony release selector '{selector}'."));
    }

    private static ResolvedSymphonyReleaseAsset SelectAsset(
        ReleaseSelector selector,
        RuntimeTarget target,
        ReleaseTag releaseTag,
        IReadOnlyList<ResolvedSymphonyReleaseAsset> assets)
    {
        SymphonyReleaseAssetKind[] preferredKinds = GetPreferredKinds(target);
        string[] operatingSystemAliases = GetOperatingSystemAliases(target.OperatingSystem);
        string[] architectureAliases = GetArchitectureAliases(target.Architecture);

        var selected = assets
            .Select((asset, index) => new AssetCandidate(
                asset,
                index,
                Array.IndexOf(preferredKinds, asset.Kind),
                GetAssetScore(asset)))
            .Where(candidate => candidate.KindPreferenceIndex >= 0)
            .Where(candidate => ContainsAnyToken(candidate.Asset.Name, operatingSystemAliases))
            .Where(candidate => ContainsAnyToken(candidate.Asset.Name, architectureAliases))
            .OrderBy(candidate => candidate.KindPreferenceIndex)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.OriginalIndex)
            .FirstOrDefault();

        if (selected is not null)
        {
            return selected.Asset;
        }

        throw new SymphonyReleaseResolutionException(
            $"No Symphony release asset compatible with {target.ExecutionMode} {target.OperatingSystem}/{target.Architecture} " +
            $"was found for selector '{selector}' resolved to '{releaseTag}'. Available assets: {FormatAssetList(assets)}.");
    }

    private static SymphonyReleaseAssetKind[] GetPreferredKinds(RuntimeTarget target)
    {
        return target.ArtifactPreference switch
        {
            RuntimeArtifactPreference.ReleaseArchive => [SymphonyReleaseAssetKind.ReleaseArchive],
            RuntimeArtifactPreference.ContainerImage => [SymphonyReleaseAssetKind.ContainerImageMetadata],
            _ when target.ExecutionMode is ExecutionMode.Docker or ExecutionMode.AzureContainer =>
                [SymphonyReleaseAssetKind.ContainerImageMetadata, SymphonyReleaseAssetKind.ReleaseArchive],
            _ => [SymphonyReleaseAssetKind.ReleaseArchive],
        };
    }

    private static string[] GetOperatingSystemAliases(string operatingSystem)
    {
        return operatingSystem switch
        {
            "windows" or "win" or "win32" => ["windows", "win", "win32"],
            "linux" => ["linux"],
            "macos" or "osx" or "darwin" or "mac" => ["macos", "osx", "darwin", "mac"],
            _ => [operatingSystem],
        };
    }

    private static string[] GetArchitectureAliases(string architecture)
    {
        return architecture switch
        {
            "x64" or "amd64" or "x86_64" or "x86-64" => ["x64", "amd64", "x86_64", "x86-64"],
            "arm64" or "aarch64" => ["arm64", "aarch64"],
            "x86" or "i386" or "i686" => ["x86", "i386", "i686"],
            _ => [architecture],
        };
    }

    private static int GetAssetScore(ResolvedSymphonyReleaseAsset asset)
    {
        int score = 0;

        if (asset.Name.StartsWith("symphony", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            asset.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (asset.ContentType?.Contains("zip", StringComparison.OrdinalIgnoreCase) == true ||
            asset.ContentType?.Contains("gzip", StringComparison.OrdinalIgnoreCase) == true ||
            asset.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            score++;
        }

        return score;
    }

    private static bool ContainsAnyToken(string value, IReadOnlyList<string> tokens)
    {
        string searchable = ToTokenSearchString(value);

        return tokens
            .Select(ToTokenSearchString)
            .Any(token => searchable.Contains(token, StringComparison.Ordinal));
    }

    private static string ToTokenSearchString(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('-');
        bool previousWasSeparator = true;

        foreach (char c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        if (!previousWasSeparator)
        {
            builder.Append('-');
        }

        return builder.ToString();
    }

    private static string FormatAssetList(IReadOnlyList<ResolvedSymphonyReleaseAsset> assets)
    {
        string[] assetNames = assets
            .Take(10)
            .Select(asset => asset.Name)
            .ToArray();
        string suffix = assets.Count > assetNames.Length ? ", ..." : string.Empty;

        return assetNames.Length == 0
            ? "none"
            : string.Join(", ", assetNames) + suffix;
    }

    private static ResolvedSymphonyReleaseAsset ToResolvedAsset(GitHubReleaseAsset asset)
    {
        string name = RequireNonEmpty(asset.Name, "GitHub release asset name");

        return new ResolvedSymphonyReleaseAsset(
            name,
            CreateAbsoluteUri(asset.BrowserDownloadUrl, $"GitHub release asset '{name}' download URL"),
            asset.Size,
            asset.ContentType,
            asset.Digest,
            ClassifyAsset(name));
    }

    private static SymphonyReleaseAssetKind ClassifyAsset(string name)
    {
        if (ContainsAnyToken(name, ["docker", "container", "image", "oci"]))
        {
            return SymphonyReleaseAssetKind.ContainerImageMetadata;
        }

        if (ContainsAnyToken(name, ["source", "src"]))
        {
            return SymphonyReleaseAssetKind.SourceArchive;
        }

        return SymphonyReleaseAssetKind.ReleaseArchive;
    }

    private static Uri CreateAbsoluteUri(string value, string fieldName)
    {
        if (!Uri.TryCreate(RequireNonEmpty(value, fieldName), UriKind.Absolute, out Uri? uri))
        {
            throw new JsonException($"{fieldName} was not an absolute URL.");
        }

        return uri;
    }

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

    private static GitHubReleaseResponse Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("GitHub returned an empty release JSON response.");
        }

        return JsonSerializer.Deserialize<GitHubReleaseResponse>(json, JsonOptions)
            ?? throw new JsonException("GitHub returned an unexpected release JSON response.");
    }

    private static string RequireNonEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private sealed record AssetCandidate(
        ResolvedSymphonyReleaseAsset Asset,
        int OriginalIndex,
        int KindPreferenceIndex,
        int Score);

    private sealed record GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAtUtc { get; init; }

        public bool Prerelease { get; init; }

        public IReadOnlyList<GitHubReleaseAsset> Assets { get; init; } = [];
    }

    private sealed record GitHubReleaseAsset
    {
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;

        [JsonPropertyName("content_type")]
        public string? ContentType { get; init; }

        public long Size { get; init; }

        public string? Digest { get; init; }
    }
}
