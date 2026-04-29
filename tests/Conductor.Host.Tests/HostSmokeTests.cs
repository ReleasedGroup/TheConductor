using Conductor.Core.Application.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Conductor.Host.Tests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<global::Program>>, IDisposable
{
    private readonly WebApplicationFactory<global::Program> factory;

    public HostSmokeTests(WebApplicationFactory<global::Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Conductor:BootstrapDevelopmentDatabase"] = "false",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDashboardProjectionStore>();
                services.AddSingleton<IDashboardProjectionStore>(
                    new StaticDashboardProjectionStore(DashboardProjection.Empty));
            });
        });
    }

    [Fact]
    public async Task LiveHealthEndpointReturnsHostStatus()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task HomePageServesDashboard()
    {
        using HttpClient client = CreateClient();

        string content = await client.GetStringAsync("/");

        Assert.Contains("Conductor Dashboard", content);
        Assert.Contains("No dashboard metrics are available yet.", content);
        Assert.Contains("Startup verification", content);
        Assert.Contains("/health/ready", content);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    public void Dispose()
    {
        factory.Dispose();
    }

    private sealed class StaticDashboardProjectionStore(DashboardProjection projection) : IDashboardProjectionStore
    {
        public Task<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(projection);
    }
}
