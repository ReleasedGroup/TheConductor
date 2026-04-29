namespace Conductor.Host.Workers;

public static class WorkerRegistrationExtensions
{
    public static IServiceCollection AddConductorWorkers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConductorWorkerOptions>(
            configuration.GetSection(ConductorWorkerOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<InstanceHealthMonitor>();
        services.AddHostedService<InstanceHealthMonitorWorker>();

        return services;
    }
}
