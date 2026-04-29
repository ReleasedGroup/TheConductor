using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
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
                services.RemoveAll<IConductorReadModelQueries>();
                services.AddSingleton<IConductorReadModelQueries>(new StubConductorReadModelQueries());
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
    public async Task HomePageServesSeededDashboard()
    {
        using HttpClient client = CreateClient();

        string content = await client.GetStringAsync("/");

        Assert.Contains("Conductor Dashboard", content);
        Assert.Contains("ReleasedGroup/TheConductor", content);
        Assert.Contains("Development fleet", content);
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

    private sealed class StubConductorReadModelQueries : IConductorReadModelQueries
    {
        private static readonly RepositoryOverview[] Repositories =
        [
            new(
                new RepositoryId(new Guid("15357d28-1f50-4a93-8f1b-aa728cc9015d")),
                "ReleasedGroup/TheConductor",
                "Conductor Platform",
                "main",
                "https://github.com/ReleasedGroup/TheConductor",
                ExecutionMode.Docker,
                InstanceLifecycleStatus.Running,
                InstanceHealthStatus.Healthy,
                "http://localhost:8010",
                DateTimeOffset.Parse("2026-04-29T00:18:00Z")),
        ];

        public Task<ConductorDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConductorDashboardSummary(1, 1, 1, 0, Repositories, []));

        public Task<IReadOnlyList<RepositoryOverview>> ListRepositoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RepositoryOverview>>(Repositories);

        public Task<RepositoryOverview?> GetRepositoryAsync(RepositoryId repositoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RepositoryOverview?>(Repositories.SingleOrDefault(repository => repository.RepositoryId == repositoryId));
    }
}
