using Conductor.Host.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conductor.Host.Tests;

public sealed class WorkerRegistrationTests
{
    [Fact]
    public void AddConductorWorkers_Registers_Instance_Collector_Worker()
    {
        Dictionary<string, string?> values = new()
        {
            ["InstanceCollector:Enabled"] = "false",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        ServiceCollection services = new();

        services.AddConductorWorkers(configuration);

        Assert.Contains(
            services,
            service => service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType == typeof(InstanceCollectorWorker));
    }
}
