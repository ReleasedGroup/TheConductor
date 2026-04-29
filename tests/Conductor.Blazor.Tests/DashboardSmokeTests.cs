using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Active_Repository_Table_From_Query()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IActiveRepositoryDashboardQuery>(
            new StubActiveRepositoryDashboardQuery(
                new ActiveRepositoryDashboard(
                [
                    new ActiveRepositoryDashboardRow(
                        "ACME Portal",
                        "releasedgroup/acme-portal",
                        DashboardRepositoryHealth.Warning,
                        9,
                        3,
                        2,
                        4,
                        DateTimeOffset.UtcNow.AddMinutes(-4)),
                ])));

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        dashboard.WaitForAssertion(() =>
        {
            Assert.Contains("Active Repositories", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("ACME Portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("releasedgroup/acme-portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Warning", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Running Agents", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Failed Runs", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("PRs Open", dashboard.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Home_Renders_Empty_State_When_No_Repositories_Are_Persisted()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IActiveRepositoryDashboardQuery>(
            new StubActiveRepositoryDashboardQuery(new ActiveRepositoryDashboard([])));

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        dashboard.WaitForAssertion(() =>
        {
            Assert.Contains("No active repositories yet", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("0 repositories", dashboard.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Home_Renders_Error_State_When_Projection_Load_Fails()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IActiveRepositoryDashboardQuery>(
            new ThrowingActiveRepositoryDashboardQuery());

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        dashboard.WaitForAssertion(() =>
        {
            Assert.Contains("Repository data is unavailable", dashboard.Markup, StringComparison.Ordinal);
        });
    }

    private sealed class StubActiveRepositoryDashboardQuery : IActiveRepositoryDashboardQuery
    {
        private readonly ActiveRepositoryDashboard dashboard;

        public StubActiveRepositoryDashboardQuery(ActiveRepositoryDashboard dashboard)
        {
            this.dashboard = dashboard;
        }

        public Task<ActiveRepositoryDashboard> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(dashboard);
    }

    private sealed class ThrowingActiveRepositoryDashboardQuery : IActiveRepositoryDashboardQuery
    {
        public Task<ActiveRepositoryDashboard> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Projection failed.");
    }
}
