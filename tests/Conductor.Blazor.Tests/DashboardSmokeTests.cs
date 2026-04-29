using Bunit;
using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Initial_Dashboard()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IConductorReadModelQueries>(new StubConductorReadModelQueries());

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        dashboard.WaitForAssertion(() =>
        {
            Assert.Contains("ReleasedGroup/TheConductor", dashboard.Markup, StringComparison.Ordinal);
        });

        Assert.Contains("Conductor Dashboard", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Development fleet", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Warning", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Startup verification", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("/health/live", dashboard.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositories_Renders_Seeded_Repository_List()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IConductorReadModelQueries>(new StubConductorReadModelQueries());

        IRenderedComponent<Repositories> repositories = context.Render<Repositories>();

        repositories.WaitForAssertion(() =>
        {
            Assert.Contains("ReleasedGroup/Symphony", repositories.Markup, StringComparison.Ordinal);
        });

        Assert.Contains("Repository registry", repositories.Markup, StringComparison.Ordinal);
        Assert.Contains("LocalProcess", repositories.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDetail_Renders_Selected_Repository()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IConductorReadModelQueries>(new StubConductorReadModelQueries());

        IRenderedComponent<RepositoryDetail> repository = context.Render<RepositoryDetail>(parameters => parameters
            .Add(component => component.RepositoryId, new Guid("15357d28-1f50-4a93-8f1b-aa728cc9015d")));

        repository.WaitForAssertion(() =>
        {
            Assert.Contains("Repository command centre", repository.Markup, StringComparison.Ordinal);
        });

        Assert.Contains("ReleasedGroup/TheConductor", repository.Markup, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8010", repository.Markup, StringComparison.Ordinal);
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
            new(
                new RepositoryId(new Guid("35592f7f-e79e-41c8-b468-e997ba3680ef")),
                "ReleasedGroup/Symphony",
                "Conductor Platform",
                "main",
                "https://github.com/ReleasedGroup/Symphony",
                ExecutionMode.LocalProcess,
                InstanceLifecycleStatus.Running,
                InstanceHealthStatus.Warning,
                "http://localhost:8020",
                DateTimeOffset.Parse("2026-04-29T00:12:00Z")),
        ];

        public Task<ConductorDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConductorDashboardSummary(1, 2, 2, 1, Repositories, Repositories[1..]));

        public Task<IReadOnlyList<RepositoryOverview>> ListRepositoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RepositoryOverview>>(Repositories);

        public Task<RepositoryOverview?> GetRepositoryAsync(RepositoryId repositoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RepositoryOverview?>(Repositories.SingleOrDefault(repository => repository.RepositoryId == repositoryId));
    }
}
