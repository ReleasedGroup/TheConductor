using System.Globalization;
using System.Text;
using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Application.Instances;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using DomainEvent = Conductor.Core.Domain.Events.Event;

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
        Repository repository = await GetOrCreateRepositoryAsync(
            metadata,
            repositoryOwner,
            repositoryName,
            now,
            cancellationToken);

        SymphonyInstanceId instanceId = SymphonyInstanceId.New();
        InstanceSnapshotId snapshotId = InstanceSnapshotId.New();
        string displayName = ResolveDisplayName(request.DisplayName, metadata, repositoryFullName, baseUri);
        ExecutionMode executionMode = ResolveExecutionMode(metadata);
        DateTimeOffset? lastSeenAtUtc = health.Status is InstanceHealthStatus.Offline or InstanceHealthStatus.Unknown
            ? null
            : now;
        string payloadJson = JsonSerializer.Serialize(new
        {
            baseUrl = baseUri.AbsoluteUri,
            repositoryFullName,
            healthStatus = health.Status.ToString(),
            runtimeMetadataAvailable = metadata.HasRepositoryMetadata,
            stateCaptured = state is not null,
        });
        StateSnapshotMetrics stateMetrics = StateSnapshotMetrics.Parse(state?.RawJson);

        SymphonyInstance instance = new(
            instanceId,
            repository.Id,
            displayName,
            executionMode,
            baseUri,
            now,
            InstanceLifecycleStatus.Running,
            health.Status,
            symphonyVersion: metadata.Version,
            symphonyReleaseTag: metadata.Version,
            lastStartedAtUtc: now,
            lastSeenAtUtc: lastSeenAtUtc);
        instance.RecordHealth(health.Status, now);

        dbContext.SymphonyInstances.Add(instance);
        dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
            snapshotId,
            instanceId,
            now,
            health.Status,
            health.RawJson,
            runtime.RawJson,
            state?.RawJson,
            stateMetrics.ActiveIssueCount,
            stateMetrics.RunningSessionCount,
            stateMetrics.RetryQueueCount,
            stateMetrics.FailedRunCount,
            stateMetrics.TokenInputTotal,
            stateMetrics.TokenOutputTotal));
        dbContext.Events.Add(new DomainEvent(
            EventId.New(),
            instanceId,
            repository.Id,
            issueNumber: null,
            EventSeverity.Information,
            "SymphonyInstanceRegistered",
            $"Registered Symphony instance {displayName}.",
            payloadJson,
            now));
        dbContext.AuditEvents.Add(new AuditEvent(
            AuditEventId.New(),
            string.IsNullOrWhiteSpace(request.RequestedByUserId) ? "system" : request.RequestedByUserId.Trim(),
            "RegisterSymphonyInstance",
            "SymphonyInstance",
            instanceId.ToString(),
            now,
            AuditEventOutcome.Succeeded,
            correlationId: null,
            message: $"Registered Symphony instance {displayName}.",
            metadataJson: payloadJson));

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualInstanceRegistrationResult(
            instanceId.ToString(),
            repository.Id.ToString(),
            repositoryFullName,
            displayName,
            baseUri,
            InstanceLifecycleStatus.Running.ToString(),
            health.Status.ToString(),
            now,
            lastSeenAtUtc,
            metadata.Version,
            snapshotId.ToString());
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
        Uri withoutSlashUri = new(withoutSlash, UriKind.Absolute);
        SymphonyInstance? existing = await dbContext.SymphonyInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                instance => instance.BaseUrl == baseUri || instance.BaseUrl == withoutSlashUri,
                cancellationToken);

        if (existing is not null)
        {
            throw new DuplicateSymphonyInstanceRegistrationException(existing.Id.ToString(), baseUri);
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

    private async Task<Repository> GetOrCreateRepositoryAsync(
        RuntimeMetadata metadata,
        string owner,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Repository? existing = await dbContext.Repositories
            .FirstOrDefaultAsync(
                repository =>
                    repository.Provider == RepositoryProvider.GitHub &&
                    repository.Owner == owner &&
                    repository.Name == name,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        Uri webUrl = metadata.WebUrl
            ?? new Uri($"https://github.com/{owner}/{name}", UriKind.Absolute);
        Uri cloneUrl = metadata.CloneUrl
            ?? new Uri($"https://github.com/{owner}/{name}.git", UriKind.Absolute);

        Repository repository = new(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            owner,
            name,
            metadata.DefaultBranch ?? "main",
            cloneUrl,
            webUrl,
            RepositoryVisibility.Public,
            isArchived: false,
            projectId: null,
            lastSyncedAtUtc: now,
            RepositoryOrchestrationStatus.Eligible,
            orchestrationStatusReason: null);

        dbContext.Repositories.Add(repository);

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
    }

    private sealed record StateSnapshotMetrics(
        int ActiveIssueCount,
        int RunningSessionCount,
        int RetryQueueCount,
        int FailedRunCount,
        long TokenInputTotal,
        long TokenOutputTotal)
    {
        public static StateSnapshotMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0);

        public static StateSnapshotMetrics Parse(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return Empty;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(rawJson);
                JsonElement root = document.RootElement;

                return new StateSnapshotMetrics(
                    ReadInt(root, "trackedIssueCount") ?? SumObjectNumbers(root, "trackedIssueDistribution"),
                    ReadArrayCount(root, "runningSessions") ?? ReadInt(root, "runningSessionCount") ?? 0,
                    ReadArrayCount(root, "retryQueue") ?? ReadInt(root, "retryQueueCount") ?? 0,
                    ReadInt(root, "failedRunCount") ?? ReadNestedInt(root, "trackedIssueDistribution", "failed") ?? 0,
                    ReadNestedLong(root, "tokens", "input") ?? ReadNestedLong(root, "tokenTotals", "input") ?? 0,
                    ReadNestedLong(root, "tokens", "output") ?? ReadNestedLong(root, "tokenTotals", "output") ?? 0);
            }
            catch (JsonException)
            {
                return Empty;
            }
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

    private static int? ReadArrayCount(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.GetArrayLength();
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value))
        {
            return Math.Max(0, value);
        }

        return null;
    }

    private static int? ReadNestedInt(JsonElement element, string parentName, string propertyName)
    {
        if (TryGetProperty(element, parentName, out JsonElement parent))
        {
            return ReadInt(parent, propertyName);
        }

        return null;
    }

    private static long? ReadNestedLong(JsonElement element, string parentName, string propertyName)
    {
        if (!TryGetProperty(element, parentName, out JsonElement parent) ||
            !TryGetProperty(parent, propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt64(out long value))
        {
            return null;
        }

        return Math.Max(0, value);
    }

    private static int SumObjectNumbers(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        int total = 0;
        foreach (JsonProperty item in property.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.Number && item.Value.TryGetInt32(out int value))
            {
                total += Math.Max(0, value);
            }
        }

        return total;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static Uri? TryParseAbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
