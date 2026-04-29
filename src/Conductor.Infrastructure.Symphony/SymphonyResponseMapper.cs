using System.Globalization;
using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;

namespace Conductor.Infrastructure.Symphony;

public static class SymphonyResponseMapper
{
    public static SymphonyHealthResponse MapHealth(
        string rawPayload,
        int? httpStatusCode,
        TimeSpan latency,
        string? errorMessage = null)
    {
        var rawStatus = ReadHealthStatus(rawPayload);
        return new SymphonyHealthResponse(
            MapHealthStatus(rawStatus, httpStatusCode, errorMessage),
            httpStatusCode,
            latency,
            NormalizeRawPayload(rawPayload))
        {
            RawStatus = rawStatus,
            ErrorMessage = TrimToNull(errorMessage)
        };
    }

    public static SymphonyRuntimeResponse MapRuntime(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;

        var application = GetObject(root, "application");
        var orchestration = GetObject(root, "orchestration");
        var persistence = GetObject(root, "persistence");
        var workflow = GetObject(root, "workflow");
        var workflowTracker = workflow.HasValue ? GetObject(workflow.Value, "tracker") : null;
        var workflowPolling = workflow.HasValue ? GetObject(workflow.Value, "polling") : null;
        var workflowAgent = workflow.HasValue ? GetObject(workflow.Value, "agent") : null;
        var workflowWorkspace = workflow.HasValue ? GetObject(workflow.Value, "workspace") : null;

        return new SymphonyRuntimeResponse(NormalizeRawPayload(rawJson))
        {
            ApplicationName = application.HasValue ? GetString(application.Value, "name") : null,
            ApplicationVersion = application.HasValue ? GetString(application.Value, "version") : null,
            InstanceId = orchestration.HasValue ? GetString(orchestration.Value, "instanceId", "instance_id") : null,
            LeaseName = orchestration.HasValue ? GetString(orchestration.Value, "leaseName", "lease_name") : null,
            LeaseTtlSeconds = orchestration.HasValue ? GetInt(orchestration.Value, "leaseTtlSeconds", "lease_ttl_seconds") : null,
            PersistenceProvider = persistence.HasValue ? GetString(persistence.Value, "provider") : null,
            PersistenceConfigured = persistence.HasValue ? GetBool(persistence.Value, "isConfigured", "is_configured") : null,
            WorkflowSourcePath = workflow.HasValue ? GetString(workflow.Value, "sourcePath", "source_path") : null,
            WorkflowOwner = workflowTracker.HasValue ? GetString(workflowTracker.Value, "owner") : null,
            WorkflowRepository = workflowTracker.HasValue ? GetString(workflowTracker.Value, "repo", "repository") : null,
            WorkflowMilestone = workflowTracker.HasValue ? GetString(workflowTracker.Value, "milestone") : null,
            PollingIntervalMs = workflowPolling.HasValue ? GetInt(workflowPolling.Value, "intervalMs", "interval_ms") : null,
            MaxConcurrentAgents = workflowAgent.HasValue
                ? GetInt(workflowAgent.Value, "maxConcurrentAgents", "max_concurrent_agents")
                : null,
            MaxTurns = workflowAgent.HasValue ? GetInt(workflowAgent.Value, "maxTurns", "max_turns") : null,
            WorkspaceRoot = workflowWorkspace.HasValue ? GetString(workflowWorkspace.Value, "root") : null,
            WorkspaceBaseBranch = workflowWorkspace.HasValue
                ? GetString(workflowWorkspace.Value, "baseBranch", "base_branch")
                : null,
            WorkflowError = GetString(root, "workflowError", "workflow_error")
        };
    }

    public static SymphonyStateResponse MapState(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        var counts = GetObject(root, "counts");
        var tracked = GetObject(root, "tracked");
        var coordination = GetObject(root, "coordination");

        return new SymphonyStateResponse(NormalizeRawPayload(rawJson))
        {
            RunningCount = counts.HasValue ? GetInt(counts.Value, "running") ?? 0 : 0,
            RetryingCount = counts.HasValue ? GetInt(counts.Value, "retrying") ?? 0 : 0,
            TrackedIssueCount = counts.HasValue ? GetInt(counts.Value, "tracked") ?? 0 : 0,
            RunningSessions = ReadArray(root, MapRunningSession, "running"),
            RetryQueue = ReadArray(root, MapRetryQueueEntry, "retrying"),
            TrackedIssueDistribution = tracked.HasValue
                ? ReadArray(tracked.Value, MapTrackedIssueStateCount, "by_state", "byState")
                : Array.Empty<SymphonyTrackedIssueStateCount>(),
            RecentActivity = ReadArray(root, MapRecentActivity, "activity"),
            Leases = coordination.HasValue
                ? ReadArray(coordination.Value, MapLeaseState, "leases")
                : Array.Empty<SymphonyLeaseState>(),
            TokenTotals = MapTokenTotals(GetObject(root, "codex_totals", "codexTotals")),
            HasRateLimits = TryGetProperty(root, out var rateLimits, "rate_limits", "rateLimits") &&
                rateLimits.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
        };
    }

    public static SymphonyIssueResponse MapIssue(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        var issueIdentifier = GetString(root, "issue_identifier", "issueIdentifier") ?? string.Empty;
        var issueId = GetString(root, "issue_id", "issueId");
        var attempts = GetObject(root, "attempts");
        var running = GetObject(root, "running");
        var retry = GetObject(root, "retry");
        var tracked = GetObject(root, "tracked");

        return new SymphonyIssueResponse(issueIdentifier, NormalizeRawPayload(rawJson))
        {
            IssueId = issueId,
            Status = GetString(root, "status"),
            WorkspacePath = GetObject(root, "workspace") is { } workspace
                ? GetString(workspace, "path")
                : null,
            RestartCount = attempts.HasValue ? GetInt(attempts.Value, "restart_count", "restartCount") ?? 0 : 0,
            CurrentRetryAttempt = attempts.HasValue
                ? GetInt(attempts.Value, "current_retry_attempt", "currentRetryAttempt")
                : null,
            Running = running.HasValue ? MapRunningSession(running.Value, issueId, issueIdentifier) : null,
            Retry = retry.HasValue ? MapRetryQueueEntry(retry.Value, issueId, issueIdentifier) : null,
            RecentEvents = ReadArray(root, MapRecentActivity, "recent_events", "recentEvents"),
            LastError = GetString(root, "last_error", "lastError"),
            Title = tracked.HasValue ? GetString(tracked.Value, "title") : null,
            Url = tracked.HasValue ? GetUri(tracked.Value, "url") : null,
            Priority = tracked.HasValue ? GetInt(tracked.Value, "priority") : null,
            CacheState = tracked.HasValue ? GetString(tracked.Value, "cache_state", "cacheState") : null,
            Milestone = tracked.HasValue ? GetString(tracked.Value, "milestone") : null,
            Labels = tracked.HasValue ? ReadStringArray(tracked.Value, "labels") : Array.Empty<string>(),
            BlockedBy = tracked.HasValue
                ? ReadArray(tracked.Value, MapBlockerReference, "blocked_by", "blockedBy")
                : Array.Empty<SymphonyBlockerReference>(),
            PullRequests = tracked.HasValue
                ? ReadArray(tracked.Value, MapPullRequestReference, "pull_requests", "pullRequests")
                : Array.Empty<SymphonyPullRequestReference>()
        };
    }

    private static SymphonyRunningSession MapRunningSession(JsonElement element)
    {
        return MapRunningSession(
            element,
            GetString(element, "issue_id", "issueId"),
            GetString(element, "issue_identifier", "issueIdentifier"));
    }

    private static SymphonyRunningSession MapRunningSession(
        JsonElement element,
        string? issueId,
        string? issueIdentifier)
    {
        return new SymphonyRunningSession(
            issueId,
            issueIdentifier,
            GetString(element, "title"),
            GetUri(element, "url"),
            GetString(element, "milestone"),
            ReadStringArray(element, "labels"),
            GetString(element, "state"),
            GetString(element, "session_id", "sessionId"),
            GetInt(element, "turn_count", "turnCount") ?? 0,
            GetString(element, "last_event", "lastEvent"),
            GetString(element, "last_message", "lastMessage"),
            GetDateTimeOffset(element, "started_at", "startedAt"),
            GetDateTimeOffset(element, "last_event_at", "lastEventAt"),
            MapTokenTotals(GetObject(element, "tokens")));
    }

    private static SymphonyRetryQueueEntry MapRetryQueueEntry(JsonElement element)
    {
        return MapRetryQueueEntry(
            element,
            GetString(element, "issue_id", "issueId"),
            GetString(element, "issue_identifier", "issueIdentifier"));
    }

    private static SymphonyRetryQueueEntry MapRetryQueueEntry(
        JsonElement element,
        string? issueId,
        string? issueIdentifier)
    {
        return new SymphonyRetryQueueEntry(
            issueId,
            issueIdentifier,
            GetString(element, "title"),
            GetUri(element, "url"),
            GetString(element, "milestone"),
            ReadStringArray(element, "labels"),
            GetInt(element, "attempt") ?? 0,
            GetDateTimeOffset(element, "due_at", "dueAt"),
            GetString(element, "error"));
    }

    private static SymphonyTrackedIssueStateCount MapTrackedIssueStateCount(JsonElement element)
    {
        return new SymphonyTrackedIssueStateCount(
            GetString(element, "state") ?? "Unknown",
            GetInt(element, "count") ?? 0);
    }

    private static SymphonyRecentActivity MapRecentActivity(JsonElement element)
    {
        return new SymphonyRecentActivity(
            GetDateTimeOffset(element, "at"),
            GetString(element, "issue_id", "issueId"),
            GetString(element, "issue_identifier", "issueIdentifier"),
            GetString(element, "session_id", "sessionId"),
            GetString(element, "level"),
            GetString(element, "event"),
            GetString(element, "message"));
    }

    private static SymphonyLeaseState MapLeaseState(JsonElement element)
    {
        return new SymphonyLeaseState(
            GetString(element, "lease_name", "leaseName"),
            GetString(element, "owner_instance_id", "ownerInstanceId"),
            GetDateTimeOffset(element, "acquired_at", "acquiredAt"),
            GetDateTimeOffset(element, "updated_at", "updatedAt"),
            GetDateTimeOffset(element, "expires_at", "expiresAt"),
            GetBool(element, "is_expired", "isExpired") ?? false);
    }

    private static SymphonyBlockerReference MapBlockerReference(JsonElement element)
    {
        return new SymphonyBlockerReference(
            GetString(element, "id"),
            GetString(element, "identifier"),
            GetString(element, "state"));
    }

    private static SymphonyPullRequestReference MapPullRequestReference(JsonElement element)
    {
        return new SymphonyPullRequestReference(
            GetString(element, "id"),
            GetInt(element, "number"),
            GetString(element, "state"),
            GetUri(element, "url"),
            GetString(element, "headRef", "head_ref"),
            GetString(element, "baseRef", "base_ref"));
    }

    private static SymphonyTokenTotals MapTokenTotals(JsonElement? element)
    {
        return element.HasValue
            ? new SymphonyTokenTotals(
                GetLong(element.Value, "input_tokens", "inputTokens") ?? 0,
                GetLong(element.Value, "output_tokens", "outputTokens") ?? 0,
                GetLong(element.Value, "total_tokens", "totalTokens") ?? 0)
            : SymphonyTokenTotals.Empty;
    }

    private static InstanceHealthStatus MapHealthStatus(
        string? rawStatus,
        int? httpStatusCode,
        string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return InstanceHealthStatus.Offline;
        }

        if (rawStatus is not null)
        {
            return rawStatus.Trim().ToLowerInvariant() switch
            {
                "healthy" or "ok" or "up" or "live" or "ready" => InstanceHealthStatus.Healthy,
                "degraded" or "warning" or "warn" => InstanceHealthStatus.Warning,
                "unhealthy" or "critical" or "error" or "failed" => InstanceHealthStatus.Critical,
                _ => httpStatusCode is >= 200 and < 300
                    ? InstanceHealthStatus.Healthy
                    : InstanceHealthStatus.Unknown
            };
        }

        return httpStatusCode switch
        {
            >= 200 and < 300 => InstanceHealthStatus.Healthy,
            >= 500 => InstanceHealthStatus.Critical,
            null => InstanceHealthStatus.Offline,
            _ => InstanceHealthStatus.Warning
        };
    }

    private static string? ReadHealthStatus(string rawPayload)
    {
        var normalized = NormalizeRawPayload(rawPayload);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(normalized);
            var root = document.RootElement;

            if (root.ValueKind is JsonValueKind.String)
            {
                return TrimToNull(root.GetString());
            }

            return GetString(root, "status", "Status");
        }
        catch (JsonException)
        {
            return TrimToNull(normalized);
        }
    }

    private static IReadOnlyList<T> ReadArray<T>(
        JsonElement element,
        Func<JsonElement, T> map,
        params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<T>();
        }

        return property.EnumerateArray().Select(map).ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property
            .EnumerateArray()
            .Select(value => value.ValueKind is JsonValueKind.String ? value.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static JsonElement? GetObject(JsonElement element, params string[] propertyNames)
    {
        return TryGetProperty(element, out var property, propertyNames) && property.ValueKind == JsonValueKind.Object
            ? property
            : null;
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => TrimToNull(property.GetString()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.GetRawText(),
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static long? GetLong(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String &&
            long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static bool? GetBool(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, params string[] propertyNames)
    {
        var value = GetString(element, propertyNames);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
                ? parsed
                : null;
    }

    private static Uri? GetUri(JsonElement element, params string[] propertyNames)
    {
        var value = GetString(element, propertyNames);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
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

    private static string NormalizeRawPayload(string rawPayload)
    {
        return rawPayload.Trim();
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
