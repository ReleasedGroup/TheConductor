using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Dashboard_Metrics_And_Active_Repository_Table()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IDashboardProjectionStore>(new StaticDashboardProjectionStore(new DashboardProjection
        {
            CapturedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
            Metrics =
            [
                Metric("healthy-repositories", "Healthy Repos", "36 / 42"),
                Metric("active-agents", "Active Agents", "18"),
                Metric("blocked-issues", "Blocked Issues", "7"),
                Metric("open-pull-requests", "PRs Open", "23"),
                Metric("ai-spend-today", "AI Spend Today", "$128.40")
            ],
            AttentionItems =
            [
                new DashboardAttentionItem
                {
                    Severity = AlertSeverity.Critical,
                    SourceName = "billing-api",
                    Summary = "2 failed runs in the last 30 minutes",
                    TargetHref = "/repositories/billing-api",
                    TargetKind = "repository",
                    CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T01:58:00Z"),
                    AgeLabel = "2m ago"
                }
            ]
        }));
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
            Assert.Contains("Conductor Dashboard", dashboard.Markup, StringComparison.Ordinal);
            Assert.Equal(5, dashboard.FindAll("[data-dashboard-metric]").Count);
            Assert.Contains("Healthy Repos", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Active Agents", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Blocked Issues", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("PRs Open", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("AI Spend Today", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Repository orchestration health", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Release Portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Needs attention", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("billing-api", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Active Repositories", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("ACME Portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("releasedgroup/acme-portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Warning", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Running Agents", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Failed Runs", dashboard.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Home_Renders_Empty_State_When_No_Repositories_Are_Persisted()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IDashboardProjectionStore>(
            new StaticDashboardProjectionStore(DashboardProjection.Empty));
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
    public void Home_Renders_Error_State_When_Repository_Projection_Load_Fails()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IDashboardProjectionStore>(
            new StaticDashboardProjectionStore(DashboardProjection.Empty));
        context.Services.AddSingleton<IActiveRepositoryDashboardQuery>(
            new ThrowingActiveRepositoryDashboardQuery());

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        dashboard.WaitForAssertion(() =>
        {
            Assert.Contains("Repository data is unavailable", dashboard.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Home_Renders_Empty_State_When_No_Metrics_Exist()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IDashboardProjectionStore>(
            new StaticDashboardProjectionStore(DashboardProjection.Empty));
        context.Services.AddSingleton<IActiveRepositoryDashboardQuery>(
            new StubActiveRepositoryDashboardQuery(new ActiveRepositoryDashboard([])));

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        Assert.Contains("No dashboard metrics are available yet.", dashboard.Markup, StringComparison.Ordinal);
    }

    private static DashboardMetric Metric(string key, string label, string value)
    {
        return new DashboardMetric
        {
            Key = key,
            Label = label,
            Value = value,
            Detail = "Sample detail",
            TrendText = "Within target",
            Icon = "pulse"
        };
    }

    private sealed class StaticDashboardProjectionStore(DashboardProjection projection) : IDashboardProjectionStore
    {
        public Task<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(projection);
        }
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
