using Conductor.Core.Application.InstanceCollection;
using Microsoft.Extensions.Options;

namespace Conductor.Host.Workers;

public sealed class InstanceCollectorWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptionsMonitor<InstanceCollectorWorkerOptions> optionsMonitor;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<InstanceCollectorWorker> logger;
    private readonly Dictionary<Guid, InstanceCollectionSchedule> schedules = [];

    public InstanceCollectorWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<InstanceCollectorWorkerOptions> optionsMonitor,
        TimeProvider timeProvider,
        ILogger<InstanceCollectorWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.optionsMonitor = optionsMonitor;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            InstanceCollectorWorkerOptions options = optionsMonitor.CurrentValue;

            if (!options.Enabled)
            {
                await DelayAsync(options.LoopDelay, stoppingToken);
                continue;
            }

            try
            {
                await CollectDueInstancesAsync(options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Instance collector loop failed.");
            }

            await DelayAsync(options.LoopDelay, stoppingToken);
        }
    }

    private async Task CollectDueInstancesAsync(
        InstanceCollectorWorkerOptions options,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IInstanceCollectionStore collectionStore = scope.ServiceProvider.GetRequiredService<IInstanceCollectionStore>();
        CollectInstanceSnapshotService collector = scope.ServiceProvider.GetRequiredService<CollectInstanceSnapshotService>();

        IReadOnlyList<CollectableSymphonyInstance> instances =
            await collectionStore.ListCollectableInstancesAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        HashSet<Guid> activeInstanceIds = instances.Select(instance => instance.Id.Value).ToHashSet();

        foreach (Guid staleInstanceId in schedules.Keys.Where(id => !activeInstanceIds.Contains(id)).ToArray())
        {
            schedules.Remove(staleInstanceId);
        }

        foreach (CollectableSymphonyInstance instance in instances)
        {
            InstanceCollectionSchedule schedule = GetOrCreateSchedule(instance.Id.Value, now);

            bool collectHealth = now >= schedule.NextHealthAtUtc;
            bool collectRuntime = now >= schedule.NextRuntimeAtUtc;
            bool collectState = now >= schedule.NextStateAtUtc;

            if (!collectHealth && !collectRuntime && !collectState)
            {
                continue;
            }

            try
            {
                await collector.CollectAsync(
                    instance,
                    new InstanceCollectionRequest(collectRuntime, collectState),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Instance collector failed for {InstanceId} at {BaseUrl}.",
                    instance.Id.Value,
                    instance.BaseUrl);
            }

            DateTimeOffset scheduledAtUtc = timeProvider.GetUtcNow();

            if (collectHealth)
            {
                schedule.NextHealthAtUtc = scheduledAtUtc + EnsurePositive(options.HealthInterval, nameof(options.HealthInterval));
            }

            if (collectRuntime)
            {
                schedule.NextRuntimeAtUtc = scheduledAtUtc + EnsurePositive(options.RuntimeInterval, nameof(options.RuntimeInterval));
            }

            if (collectState)
            {
                schedule.NextStateAtUtc = scheduledAtUtc + EnsurePositive(options.StateInterval, nameof(options.StateInterval));
            }
        }
    }

    private InstanceCollectionSchedule GetOrCreateSchedule(Guid instanceId, DateTimeOffset now)
    {
        if (schedules.TryGetValue(instanceId, out InstanceCollectionSchedule? schedule))
        {
            return schedule;
        }

        schedule = new InstanceCollectionSchedule(now, now, now);
        schedules[instanceId] = schedule;

        return schedule;
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(EnsurePositive(delay, nameof(delay)), timeProvider, cancellationToken);
    }

    private static TimeSpan EnsurePositive(TimeSpan value, string name)
    {
        if (value > TimeSpan.Zero)
        {
            return value;
        }

        throw new InvalidOperationException($"{name} must be greater than zero.");
    }

    private sealed class InstanceCollectionSchedule(
        DateTimeOffset nextHealthAtUtc,
        DateTimeOffset nextRuntimeAtUtc,
        DateTimeOffset nextStateAtUtc)
    {
        public DateTimeOffset NextHealthAtUtc { get; set; } = nextHealthAtUtc;

        public DateTimeOffset NextRuntimeAtUtc { get; set; } = nextRuntimeAtUtc;

        public DateTimeOffset NextStateAtUtc { get; set; } = nextStateAtUtc;
    }
}
