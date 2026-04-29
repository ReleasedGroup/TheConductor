using System.Text.Json;
using Conductor.Core.Application.InstanceCollection;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Events;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Repositories;

public sealed class SqliteInstanceCollectionStore : IInstanceCollectionStore
{
    private const string HealthChangedEventType = "InstanceHealthChanged";
    private const string OfflineAlertSource = "InstanceCollector";
    private const string OfflineAlertSummary = "Symphony instance offline";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConductorDbContext dbContext;

    public SqliteInstanceCollectionStore(ConductorDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CollectableSymphonyInstance>> ListCollectableInstancesAsync(
        CancellationToken cancellationToken)
    {
        List<SymphonyInstance> instances = await dbContext.SymphonyInstances
            .AsNoTracking()
            .Where(instance => instance.LifecycleStatus != InstanceLifecycleStatus.Destroyed)
            .OrderBy(instance => instance.DisplayName)
            .ToListAsync(cancellationToken);

        return instances
            .Select(instance => new CollectableSymphonyInstance(
                instance.Id,
                instance.RepositoryId,
                instance.DisplayName,
                instance.BaseUrl,
                instance.LifecycleStatus,
                instance.HealthStatus,
                instance.LastHealthCheckAtUtc,
                instance.LastSeenAtUtc))
            .ToArray();
    }

    public async Task SaveCollectionResultAsync(
        InstanceCollectionResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        SymphonyInstance? instance = await dbContext.SymphonyInstances
            .SingleOrDefaultAsync(row => row.Id == result.InstanceId, cancellationToken);

        if (instance is null)
        {
            return;
        }

        InstanceHealthStatus previousHealthStatus = instance.HealthStatus;
        instance.RecordHealth(result.HealthStatus, result.CapturedAtUtc);

        dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
            InstanceSnapshotId.New(),
            result.InstanceId,
            result.CapturedAtUtc,
            result.HealthStatus,
            result.HealthJson,
            result.RuntimeJson,
            result.StateJson,
            activeIssueCount: 0,
            runningSessionCount: 0,
            retryQueueCount: 0,
            failedRunCount: 0,
            tokenInputTotal: 0,
            tokenOutputTotal: 0));

        if (previousHealthStatus != result.HealthStatus)
        {
            AddHealthChangedEvent(instance, previousHealthStatus, result);
        }

        if (result.HealthStatus == InstanceHealthStatus.Offline)
        {
            await AddOfflineAlertIfMissingAsync(instance, result, cancellationToken);
        }
        else if (previousHealthStatus == InstanceHealthStatus.Offline)
        {
            await ResolveOfflineAlertsAsync(instance.Id, result.CapturedAtUtc, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddHealthChangedEvent(
        SymphonyInstance instance,
        InstanceHealthStatus previousHealthStatus,
        InstanceCollectionResult result)
    {
        dbContext.Events.Add(new Event(
            EventId.New(),
            instance.Id,
            instance.RepositoryId,
            issueNumber: null,
            MapEventSeverity(result.HealthStatus),
            HealthChangedEventType,
            TrimToLength(
                $"{instance.DisplayName} health changed from {previousHealthStatus} to {result.HealthStatus}.",
                1000),
            JsonSerializer.Serialize(
                new
                {
                    previousHealthStatus,
                    currentHealthStatus = result.HealthStatus,
                    result.HttpStatusCode,
                    result.LatencyMilliseconds,
                    result.ErrorMessage,
                },
                JsonSerializerOptions),
            result.CapturedAtUtc));
    }

    private async Task AddOfflineAlertIfMissingAsync(
        SymphonyInstance instance,
        InstanceCollectionResult result,
        CancellationToken cancellationToken)
    {
        bool hasActiveOfflineAlert = await dbContext.Alerts
            .AnyAsync(alert =>
                alert.SymphonyInstanceId == instance.Id &&
                alert.Status == AlertStatus.Active &&
                alert.Source == OfflineAlertSource &&
                alert.Summary == OfflineAlertSummary,
                cancellationToken);

        if (hasActiveOfflineAlert)
        {
            return;
        }

        dbContext.Alerts.Add(new Alert(
            AlertId.New(),
            AlertSeverity.Critical,
            OfflineAlertSource,
            OfflineAlertSummary,
            TrimToLength(
                $"{instance.DisplayName} could not be reached by the instance collector.",
                1000),
            result.CapturedAtUtc,
            instance.Id,
            instance.RepositoryId));
    }

    private async Task ResolveOfflineAlertsAsync(
        SymphonyInstanceId instanceId,
        DateTimeOffset resolvedAtUtc,
        CancellationToken cancellationToken)
    {
        Alert[] alerts = await dbContext.Alerts
            .Where(alert =>
                alert.SymphonyInstanceId == instanceId &&
                alert.Status == AlertStatus.Active &&
                alert.Source == OfflineAlertSource &&
                alert.Summary == OfflineAlertSummary)
            .ToArrayAsync(cancellationToken);

        foreach (Alert alert in alerts)
        {
            alert.Resolve(resolvedAtUtc, "Instance health recovered.");
        }
    }

    private static EventSeverity MapEventSeverity(InstanceHealthStatus status) => status switch
    {
        InstanceHealthStatus.Warning => EventSeverity.Warning,
        InstanceHealthStatus.Critical => EventSeverity.Error,
        InstanceHealthStatus.Offline => EventSeverity.Critical,
        _ => EventSeverity.Information,
    };

    private static string TrimToLength(string value, int maxLength)
    {
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
