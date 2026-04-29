using Microsoft.AspNetCore.Mvc.Testing;

namespace Conductor.Host.Tests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> factory;

    public HostSmokeTests(WebApplicationFactory<global::Program> factory)
    {
        this.factory = factory;
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
    {
        using HttpClient client = CreateClient();

        string content = await client.GetStringAsync("/");

        Assert.Contains("Dashboard", content);
        Assert.Contains("Healthy Repos", content);
        Assert.Contains("Repository orchestration health", content);
        Assert.Contains("Needs attention", content);
        Assert.Contains("Active Repositories", content);
        Assert.Contains("Live activity", content);
        Assert.Contains("Tests failed and a continuation run started.", content);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
}
