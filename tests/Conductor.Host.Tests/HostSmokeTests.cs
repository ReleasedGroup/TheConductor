using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Conductor.Host.Tests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<global::Program>>, IDisposable
{
    private readonly string databasePath = CreateTemporaryDatabasePath();
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
                    ["ConnectionStrings:Conductor"] = $"Data Source={databasePath};Cache=Shared",
                    ["Conductor:BootstrapDevelopmentDatabase"] = "false",
                    ["InstanceCollector:Enabled"] = "false",
                });
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
    public async Task HomePageServesProjectionDashboardWithLiveActivity()
    public async Task HomePageServesDashboardWithActiveRepositoriesAndLiveActivity()
    {
        using HttpClient client = CreateClient();

        string content = await client.GetStringAsync("/");

        Assert.Contains("Dashboard", content);
        Assert.Contains("Healthy Repos", content);
        Assert.Contains("Repository orchestration health", content);
        Assert.Contains("Needs attention", content);
        Assert.Contains("Active Repositories", content);
        Assert.Contains("Conductor Dashboard", content);
        Assert.Contains("Active Repositories", content);
        Assert.Contains("Repository health, workload, pull requests, failures", content);
        Assert.Contains("Live activity", content);
        Assert.Contains("Tests failed and a continuation run started.", content);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    public void Dispose()
    {
        factory.Dispose();

        string? databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory) && Directory.Exists(databaseDirectory))
        {
            Directory.Delete(databaseDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            "conductor-host-tests",
            Guid.NewGuid().ToString("N"),
            "conductor.db");
}
