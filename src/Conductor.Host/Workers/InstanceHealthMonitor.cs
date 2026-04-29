using System.Text.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainEvent = Conductor.Core.Domain.Events.Event;
using DomainEventId = Conductor.Core.Domain.Ids.EventId;

namespace Conductor.Host.Workers;

public sealed class InstanceHealthMonitor(
    ConductorDbContext dbContext,
    ISymphonyApiClient symphonyApiClient,
    TimeProvider timeProvider,
    ILogger<InstanceHealthMonitor> logger)
{
    public const string OfflineAlertSource = "symphony-instance-health";
    public const string OfflineEventType = "InstanceOffline";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> PollOnceAsync(CancellationToken cancellationToken = default)
    {
        List<SymphonyInstanceId> instanceIds = await dbContext.SymphonyInstances
            .AsNoTracking()
            .Where(instance =>
                instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed &&
                instance.LifecycleStatus != InstanceLifecycleStatus.Stopped &&
                instance.LifecycleStatus != InstanceLifecycleStatus.Stopping)
            .OrderBy(instance => instance.DisplayName)
            .Select(instance => instance.Id)
            .ToListAsync(cancellationToken);

        int polledCount = 0;

        foreach (SymphonyInstanceId instanceId in instanceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await PollInstanceAsync(instanceId, cancellationToken))
                {
                    polledCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to poll Symphony instance {SymphonyInstanceId}.",
                    instanceId);
            }
            finally
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return polledCount;
    }

    private async Task<bool> PollInstanceAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken)
    {
        SymphonyInstance? instance = await dbContext.SymphonyInstances
            .SingleOrDefaultAsync(candidate => candidate.Id == instanceId, cancellationToken);

        if (instance is null || !CanPoll(instance.LifecycleStatus))
        {
            return false;
        }

        InstanceHealthStatus previousHealthStatus = instance.HealthStatus;
        SymphonyHealthResponse health = await GetHealthSafelyAsync(instance, cancellationToken);
        DateTimeOffset observedAtUtc = timeProvider.GetUtcNow();

        instance.RecordHealth(health.Status, observedAtUtc);
        dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
            InstanceSnapshotId.New(),
            instance.Id,
            observedAtUtc,
            health.Status,
            health.RawJson,
            runtimeJson: null,
            stateJson: null,
            activeIssueCount: 0,
            runningSessionCount: 0,
            retryQueueCount: 0,
            failedRunCount: 0,
            tokenInputTotal: 0,
            tokenOutputTotal: 0));

        if (ShouldRaiseOfflineAlert(previousHealthStatus, health.Status) &&
            !await HasUnresolvedOfflineAlertAsync(instance.Id, cancellationToken))
        {
            AddOfflineEventAndAlert(instance, health, observedAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<SymphonyHealthResponse> GetHealthSafelyAsync(
        SymphonyInstance instance,
        CancellationToken cancellationToken)
    {
        try
        {
            return await symphonyApiClient.GetHealthAsync(instance.BaseUrl, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Symphony health request failed for instance {SymphonyInstanceId}.",
                instance.Id);

            return new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                HttpStatusCode: null,
                Latency: TimeSpan.Zero,
                CreateFailureJson(exception));
        }
    }

    private async Task<bool> HasUnresolvedOfflineAlertAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken) =>
        await dbContext.Alerts.AnyAsync(
            alert =>
                alert.SymphonyInstanceId != null &&
                alert.SymphonyInstanceId == instanceId &&
                alert.Source == OfflineAlertSource &&
                alert.Status != AlertStatus.Resolved,
            cancellationToken);

    private void AddOfflineEventAndAlert(
        SymphonyInstance instance,
        SymphonyHealthResponse health,
        DateTimeOffset observedAtUtc)
    {
        string summary = $"Symphony instance '{instance.DisplayName}' is offline";
        string recommendedAction =
            "Check the instance process or container, network connectivity, and configured base URL.";

        dbContext.Events.Add(new DomainEvent(
            DomainEventId.New(),
            instance.Id,
            instance.RepositoryId,
            issueNumber: null,
            EventSeverity.Critical,
            OfflineEventType,
            $"Symphony instance '{instance.DisplayName}' became unreachable.",
            CreateOfflinePayload(instance, health),
            observedAtUtc));

        dbContext.Alerts.Add(new Alert(
            AlertId.New(),
            AlertSeverity.Critical,
            OfflineAlertSource,
            summary,
            recommendedAction,
            observedAtUtc,
            instance.Id,
            instance.RepositoryId));
    }

    private static bool CanPoll(InstanceLifecycleStatus lifecycleStatus) =>
        lifecycleStatus is not InstanceLifecycleStatus.Destroyed
            and not InstanceLifecycleStatus.Stopped
            and not InstanceLifecycleStatus.Stopping;

    private static bool ShouldRaiseOfflineAlert(
        InstanceHealthStatus previousHealthStatus,
        InstanceHealthStatus currentHealthStatus) =>
        currentHealthStatus == InstanceHealthStatus.Offline &&
        previousHealthStatus != InstanceHealthStatus.Offline;

    private static string CreateOfflinePayload(
        SymphonyInstance instance,
        SymphonyHealthResponse health) =>
        JsonSerializer.Serialize(
            new OfflineEventPayload(
                instance.Id.ToString(),
                instance.DisplayName,
                instance.BaseUrl.ToString(),
                health.HttpStatusCode,
                health.Latency.TotalMilliseconds,
                health.RawJson),
            JsonOptions);

    private static string CreateFailureJson(Exception exception) =>
        JsonSerializer.Serialize(
            new HealthPollFailure("poll_failed", exception.Message),
            JsonOptions);

    private sealed record OfflineEventPayload(
        string SymphonyInstanceId,
        string DisplayName,
        string BaseUrl,
        int? HttpStatusCode,
        double LatencyMilliseconds,
        string HealthRawJson);

    private sealed record HealthPollFailure(string Kind, string Message);
}
