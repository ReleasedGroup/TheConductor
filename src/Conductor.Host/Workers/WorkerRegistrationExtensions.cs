using Conductor.Core.Application.InstanceCollection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conductor.Host.Workers;

public static class WorkerRegistrationExtensions
{
    public static IServiceCollection AddConductorWorkers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConductorWorkerOptions>(
            configuration.GetSection(ConductorWorkerOptions.SectionName));
        services.Configure<InstanceCollectorWorkerOptions>(
            configuration.GetSection(InstanceCollectorWorkerOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<InstanceHealthMonitor>();
        services.AddScoped<CollectInstanceSnapshotService>();
        services.AddHostedService<InstanceHealthMonitorWorker>();
        services.AddHostedService<InstanceCollectorWorker>();

        return services;
    }
}
