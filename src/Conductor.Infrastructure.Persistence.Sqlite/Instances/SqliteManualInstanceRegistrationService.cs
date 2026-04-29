using System.Globalization;
using System.Text;
using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Application.Instances;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Persistence.Sqlite.Schema;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Instances;

internal sealed class SqliteManualInstanceRegistrationService : IManualInstanceRegistrationService
{
    private const string ManualRepositoryOwner = "manual";
    private readonly ConductorDbContext dbContext;
    private readonly ISymphonyApiClient symphonyApiClient;
    private readonly TimeProvider timeProvider;

    public SqliteManualInstanceRegistrationService(
        ConductorDbContext dbContext,
        ISymphonyApiClient symphonyApiClient,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.symphonyApiClient = symphonyApiClient;
        this.timeProvider = timeProvider;
    }

    public async Task<ManualInstanceRegistrationResult> RegisterAsync(
        ManualInstanceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        Uri baseUri = ValidateAndNormalizeBaseUri(request);
        await ThrowIfDuplicateAsync(baseUri, cancellationToken);

        SymphonyHealthResponse health = await ProbeHealthAsync(baseUri, cancellationToken);
        SymphonyRuntimeResponse runtime = await ProbeRuntimeAsync(baseUri, cancellationToken);
        SymphonyStateResponse? state = await TryProbeStateAsync(baseUri, cancellationToken);
        RuntimeMetadata metadata = RuntimeMetadata.Parse(runtime.RawJson);

        DateTimeOffset now = timeProvider.GetUtcNow();
        string repositoryOwner = metadata.Owner ?? ManualRepositoryOwner;
        string repositoryName = metadata.RepositoryName ?? CreateManualRepositoryName(baseUri);
        string repositoryFullName = $"{repositoryOwner}/{repositoryName}";
        RepositoryRecord repository = await GetOrCreateRepositoryAsync(
            metadata,
            repositoryOwner,
            repositoryName,
            now,
            cancellationToken);

        string instanceId = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        string snapshotId = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        string displayName = ResolveDisplayName(request.DisplayName, metadata, repositoryFullName, baseUri);
        string healthStatus = health.Status.ToString();
        string lifecycleStatus = InstanceLifecycleStatus.Running.ToString();
        DateTimeOffset? lastSeenAtUtc = health.Status is InstanceHealthStatus.Offline or InstanceHealthStatus.Unknown
            ? null
            : now;

        dbContext.Set<SymphonyInstanceRecord>().Add(new SymphonyInstanceRecord
        {
            Id = instanceId,
            RepositoryId = repository.Id,
            DisplayName = displayName,
            ExecutionMode = ResolveExecutionMode(metadata).ToString(),
            BaseUrl = baseUri.AbsoluteUri,
            Status = lifecycleStatus,
            HealthStatus = healthStatus,
            DeliveryStatus = "Healthy",
            ResolvedReleaseTag = metadata.Version,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastHealthCheckAtUtc = now,
            LastSeenAtUtc = lastSeenAtUtc,
        });

        dbContext.Set<InstanceSnapshotRecord>().Add(new InstanceSnapshotRecord
        {
            Id = snapshotId,
            SymphonyInstanceId = instanceId,
            CapturedAtUtc = now,
            HealthStatus = healthStatus,
            HttpStatusCode = health.HttpStatusCode,
            LatencyMilliseconds = (long)Math.Round(health.Latency.TotalMilliseconds),
            HealthJson = health.RawJson,
            RuntimeJson = runtime.RawJson,
            StateJson = state?.RawJson,
        });

        dbContext.Set<EventRecord>().Add(new EventRecord
        {
            Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
            SymphonyInstanceId = instanceId,
            RepositoryId = repository.Id,
            EventType = "SymphonyInstanceRegistered",
            Severity = EventSeverity.Information.ToString(),
            Message = $"Registered Symphony instance {displayName}.",
            OccurredAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(new
            {
                baseUrl = baseUri.AbsoluteUri,
                repositoryFullName,
                healthStatus,
                runtimeMetadataAvailable = metadata.HasRepositoryMetadata,
                stateCaptured = state is not null,
            }),
        });

        dbContext.Set<AuditEventRecord>().Add(new AuditEventRecord
        {
            Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
            ActorUserId = string.IsNullOrWhiteSpace(request.RequestedByUserId)
                ? "system"
                : request.RequestedByUserId.Trim(),
            Action = "RegisterSymphonyInstance",
            ResourceType = "SymphonyInstance",
            ResourceId = instanceId,
            OccurredAtUtc = now,
            MetadataJson = JsonSerializer.Serialize(new
            {
                baseUrl = baseUri.AbsoluteUri,
                repositoryFullName,
                healthStatus,
            }),
        });

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualInstanceRegistrationResult(
            instanceId,
            repository.Id,
            repositoryFullName,
            displayName,
            baseUri,
            lifecycleStatus,
            healthStatus,
            now,
            lastSeenAtUtc,
            metadata.Version,
            snapshotId);
    }

    private static Uri ValidateAndNormalizeBaseUri(ManualInstanceRegistrationRequest request)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal)
        {
            ["baseUrl"] = [],
        };

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            errors["baseUrl"] = ["A Symphony instance URL is required."];
            throw new ManualInstanceRegistrationValidationException(errors);
        }

        if (!Uri.TryCreate(request.BaseUrl.Trim(), UriKind.Absolute, out Uri? parsed) ||
            parsed.Scheme is not "http" and not "https")
        {
            errors["baseUrl"] = ["Enter an absolute HTTP or HTTPS URL."];
            throw new ManualInstanceRegistrationValidationException(errors);
        }

        if (!string.IsNullOrWhiteSpace(parsed.Query) || !string.IsNullOrWhiteSpace(parsed.Fragment))
        {
            errors["baseUrl"] = ["The URL cannot include a query string or fragment."];
            throw new ManualInstanceRegistrationValidationException(errors);
        }

        return NormalizeBaseUri(parsed);
    }

    private static Uri NormalizeBaseUri(Uri baseUri)
    {
        UriBuilder builder = new(baseUri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
        };

        string path = builder.Path.TrimEnd('/');
        string[] apiSuffixes =
        [
            "/api/v1/health",
            "/api/v1/runtime",
            "/api/v1/state",
            "/api/v1/refresh",
        ];

        foreach (string suffix in apiSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[..^suffix.Length];
                break;
            }
        }

        builder.Path = string.IsNullOrWhiteSpace(path) ? "/" : path.TrimEnd('/') + "/";

        return builder.Uri;
    }

    private async Task ThrowIfDuplicateAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        string withSlash = baseUri.AbsoluteUri;
        string withoutSlash = withSlash.TrimEnd('/');
        SymphonyInstanceRecord? existing = await dbContext.Set<SymphonyInstanceRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                instance => instance.BaseUrl == withSlash || instance.BaseUrl == withoutSlash,
                cancellationToken);

        if (existing is not null)
        {
            throw new DuplicateSymphonyInstanceRegistrationException(existing.Id, baseUri);
        }
    }

    private async Task<SymphonyHealthResponse> ProbeHealthAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        SymphonyHealthResponse health;

        try
        {
            health = await symphonyApiClient.GetHealthAsync(baseUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ManualInstanceRegistrationValidationException(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["health"] = [$"The Symphony health endpoint could not be reached: {ex.Message}"],
                });
        }

        if (health.HttpStatusCode is < 200 or > 299 || health.Status == InstanceHealthStatus.Offline)
        {
            throw new ManualInstanceRegistrationValidationException(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["health"] =
                    [
                        $"The Symphony health endpoint returned {health.HttpStatusCode?.ToString(CultureInfo.InvariantCulture) ?? "no HTTP status"} with {health.Status} health.",
                    ],
                });
        }

        return health;
    }

    private async Task<SymphonyRuntimeResponse> ProbeRuntimeAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        try
        {
            return await symphonyApiClient.GetRuntimeAsync(baseUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ManualInstanceRegistrationValidationException(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["runtime"] = [$"The Symphony runtime endpoint could not be reached: {ex.Message}"],
                });
        }
    }

    private async Task<SymphonyStateResponse?> TryProbeStateAsync(
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        try
        {
            return await symphonyApiClient.GetStateAsync(baseUri, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private async Task<RepositoryRecord> GetOrCreateRepositoryAsync(
        RuntimeMetadata metadata,
        string owner,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        RepositoryRecord? existing = await dbContext.Set<RepositoryRecord>()
            .FirstOrDefaultAsync(
                repository =>
                    repository.Provider == RepositoryProvider.GitHub.ToString() &&
                    repository.Owner == owner &&
                    repository.Name == name,
                cancellationToken);

        if (existing is not null)
        {
            existing.UpdatedAtUtc = now;
            return existing;
        }

        Uri webUrl = metadata.WebUrl
            ?? new Uri($"https://github.com/{owner}/{name}", UriKind.Absolute);
        Uri cloneUrl = metadata.CloneUrl
            ?? new Uri($"https://github.com/{owner}/{name}.git", UriKind.Absolute);

        RepositoryRecord repository = new()
        {
            Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
            Provider = RepositoryProvider.GitHub.ToString(),
            Owner = owner,
            Name = name,
            DefaultBranch = metadata.DefaultBranch ?? "main",
            CloneUrl = cloneUrl.AbsoluteUri,
            WebUrl = webUrl.AbsoluteUri,
            IsArchived = false,
            OpenIssueCount = 0,
            PullRequestCount = 0,
            ImportedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dbContext.Set<RepositoryRecord>().Add(repository);

        return repository;
    }

    private static string ResolveDisplayName(
        string? requestedDisplayName,
        RuntimeMetadata metadata,
        string repositoryFullName,
        Uri baseUri)
    {
        if (!string.IsNullOrWhiteSpace(requestedDisplayName))
        {
            return requestedDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(metadata.InstanceId))
        {
            return metadata.InstanceId.Trim();
        }

        return metadata.HasRepositoryMetadata
            ? repositoryFullName
            : baseUri.Authority;
    }

    private static ExecutionMode ResolveExecutionMode(RuntimeMetadata metadata)
    {
        if (metadata.ExecutionMode is not null &&
            Enum.TryParse(metadata.ExecutionMode, ignoreCase: true, out ExecutionMode parsed))
        {
            return parsed;
        }

        return ExecutionMode.LocalProcess;
    }

    private static string CreateManualRepositoryName(Uri baseUri)
    {
        string source = baseUri.IsDefaultPort
            ? baseUri.Host
            : $"{baseUri.Host}-{baseUri.Port.ToString(CultureInfo.InvariantCulture)}";
        string path = baseUri.AbsolutePath.Trim('/');

        if (!string.IsNullOrWhiteSpace(path))
        {
            source += "-" + path;
        }

        StringBuilder builder = new(source.Length);
        bool previousWasDash = false;

        foreach (char value in source.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(value))
            {
                builder.Append(value);
                previousWasDash = false;
            }
            else if (!previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        string sanitized = builder.ToString().Trim('-');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "registered-instance"
            : sanitized[..Math.Min(sanitized.Length, 96)];
    }

    private sealed record RuntimeMetadata(
        string? Version,
        string? InstanceId,
        string? Owner,
        string? RepositoryName,
        string? DefaultBranch,
        Uri? CloneUrl,
        Uri? WebUrl,
        string? ExecutionMode)
    {
        public bool HasRepositoryMetadata => Owner is not null && RepositoryName is not null;

        public static RuntimeMetadata Parse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new RuntimeMetadata(null, null, null, null, null, null, null, null);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(rawJson);
                JsonElement root = document.RootElement;
                string? fullName = FindFirstString(root, "repositoryFullName", "repoFullName", "fullName");
                (string? owner, string? name) = SplitRepositoryFullName(fullName);

                owner ??= FindFirstString(root, "repositoryOwner", "repoOwner", "owner");
                name ??= FindFirstString(root, "repositoryName", "repoName", "name", "repository");

                if (name is not null && name.Contains('/', StringComparison.Ordinal))
                {
                    (owner, name) = SplitRepositoryFullName(name);
                }

                return new RuntimeMetadata(
                    FindFirstString(root, "version", "applicationVersion", "appVersion"),
                    FindFirstString(root, "instanceId", "orchestrationInstanceId"),
                    TrimToNull(owner),
                    TrimToNull(name),
                    FindFirstString(root, "defaultBranch", "branch"),
                    TryParseAbsoluteUri(FindFirstString(root, "cloneUrl")),
                    TryParseAbsoluteUri(FindFirstString(root, "webUrl", "htmlUrl", "repositoryUrl")),
                    FindFirstString(root, "executionMode", "runnerMode"));
            }
            catch (JsonException)
            {
                return new RuntimeMetadata(null, null, null, null, null, null, null, null);
            }
        }

        private static (string? Owner, string? Name) SplitRepositoryFullName(string? fullName)
        {
            string? trimmed = TrimToNull(fullName);
            if (trimmed is null)
            {
                return (null, null);
            }

            string[] parts = trimmed.Split('/', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            return parts.Length == 2 ? (parts[0], parts[1]) : (null, null);
        }

        private static string? FindFirstString(JsonElement element, params string[] names)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return TrimToNull(property.Value.GetString());
                    }

                    string? nested = FindFirstString(property.Value, names);
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
                    string? nested = FindFirstString(item, names);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }

        private static Uri? TryParseAbsoluteUri(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;

        private static string? TrimToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
