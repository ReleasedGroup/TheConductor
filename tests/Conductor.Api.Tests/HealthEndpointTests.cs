using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Conductor.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"conductor-api-tests-{Guid.NewGuid():N}.db");

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
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
    public async Task LiveHealth_Returns_Success()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ReadyHealth_Returns_Success()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.True(response.IsSuccessStatusCode);
    }

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
