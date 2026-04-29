using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Conductor.Host.Tests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<global::Program>>, IDisposable
{
    private readonly WebApplicationFactory<global::Program> factory;
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"conductor-host-tests-{Guid.NewGuid():N}.db");

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
    public async Task HomePageServesPlaceholderDashboard()
    {
        using HttpClient client = CreateClient();

        string content = await client.GetStringAsync("/");

        Assert.Contains("Conductor Dashboard", content);
        Assert.Contains("ReleasedGroup/TheConductor", content);
        Assert.Contains("Development fleet", content);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    public void Dispose()
    {
        factory.Dispose();

        string directory = Path.GetDirectoryName(databasePath) ?? Path.GetTempPath();
        string fileName = Path.GetFileName(databasePath);

        foreach (string path in Directory.EnumerateFiles(directory, $"{fileName}*"))
        {
            File.Delete(path);
        }
    }
}
