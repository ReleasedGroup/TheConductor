using Microsoft.Extensions.Options;

namespace Conductor.Host.Workers;

public sealed class InstanceHealthMonitorWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ConductorWorkerOptions> options,
    ILogger<InstanceHealthMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(options.Value.HealthPollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PollInstancesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PollInstancesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            InstanceHealthMonitor monitor = scope.ServiceProvider.GetRequiredService<InstanceHealthMonitor>();

            await monitor.PollOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Symphony instance health polling failed.");
        }
    }
}
