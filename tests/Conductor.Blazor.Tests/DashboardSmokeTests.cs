using AngleSharp.Dom;
using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Host.Components.Dashboard;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Dashboard_Metrics_And_Active_Repository_Table()
    {
        using BunitContext context = new();
        RegisterHomeServices(
            context,
            new DashboardProjection
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
                    Attention(
                        AlertSeverity.Critical,
                        "billing-api",
                        "2 failed runs in the last 30 minutes",
                        "/repositories/billing-api",
                        "repository"),
                    Attention(
                        AlertSeverity.Warning,
                        "client-mobile",
                        "Symphony instance is offline",
                        "/instances/client-mobile",
                        "instance")
                ]
            },
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
            ]));

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
            Assert.Contains("SQLite persistence registration", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Repository orchestration health", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Release Portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Needs attention", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("billing-api", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("href=\"/repositories/billing-api\"", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("href=\"/instances/client-mobile\"", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Active Repositories", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("ACME Portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("releasedgroup/acme-portal", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Running Agents", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Failed Runs", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("Startup verification", dashboard.Markup, StringComparison.Ordinal);
            Assert.Contains("/health/live", dashboard.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Home_Renders_Empty_State_When_No_Repositories_Are_Persisted()
    {
        using BunitContext context = new();
        RegisterHomeServices(context, DashboardProjection.Empty, new ActiveRepositoryDashboard([]));

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
    public void Home_Renders_Dashboard_Metric_Tiles_From_Projection()
    {
        using BunitContext context = new();
        RegisterHomeServices(
            context,
            new DashboardProjection
            {
                CapturedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
                Metrics =
                [
                    Metric("healthy-repositories", "Healthy Repos", "36 / 42"),
                    Metric("active-agents", "Active Agents", "18"),
                    Metric("blocked-issues", "Blocked Issues", "7"),
                    Metric("open-pull-requests", "PRs Open", "23"),
                    Metric("ai-spend-today", "AI Spend Today", "$128.40")
                ]
            });

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        IElement metricGrid = dashboard.Find("section[aria-label='Dashboard metrics']");
        IReadOnlyList<IElement> tiles = dashboard.FindAll("[data-dashboard-metric]");

        Assert.Equal("Dashboard metrics", metricGrid.GetAttribute("aria-label"));
        Assert.Collection(
            tiles,
            tile => AssertMetricTile(tile, "healthy-repositories", "Healthy Repos", "36 / 42"),
            tile => AssertMetricTile(tile, "active-agents", "Active Agents", "18"),
            tile => AssertMetricTile(tile, "blocked-issues", "Blocked Issues", "7"),
            tile => AssertMetricTile(tile, "open-pull-requests", "PRs Open", "23"),
            tile => AssertMetricTile(tile, "ai-spend-today", "AI Spend Today", "$128.40"));
    }

    [Fact]
    public void Home_Renders_Startup_Verification_Cards()
    {
        using BunitContext context = new();
        RegisterHomeServices(context, DashboardProjection.Empty);

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        IReadOnlyList<IElement> checks = dashboard.FindAll(".startup-panel dl > div");

        Assert.Collection(
            checks,
            check => AssertStartupCheck(check, "Dashboard route", "/", "Metric tiles served"),
            check => AssertStartupCheck(check, "Live health", "/health/live", "Probe available"),
            check => AssertStartupCheck(check, "Ready health", "/health/ready", "Probe available"));
    }

    [Fact]
    public void Home_Renders_Empty_State_When_No_Metrics_Exist()
    {
        using BunitContext context = new();
        RegisterHomeServices(context, DashboardProjection.Empty);

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        Assert.Contains("No dashboard metrics are available yet.", dashboard.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NeedsAttentionPanel_Renders_Critical_And_Warning_Source_Links()
    {
        using BunitContext context = new();
        DashboardAttentionItem[] items =
        [
            Attention(
                AlertSeverity.Info,
                "release-notes",
                "Daily digest is available",
                "/reports/daily",
                "report"),
            Attention(
                AlertSeverity.Warning,
                "client-mobile",
                "Symphony instance is offline",
                "/instances/client-mobile",
                "instance"),
            Attention(
                AlertSeverity.Critical,
                "billing-api",
                "2 failed runs in the last 30 minutes",
                "/repositories/billing-api",
                "repository"),
        ];

        IRenderedComponent<NeedsAttentionPanel> panel = context.Render<NeedsAttentionPanel>(
            parameters => parameters.Add(component => component.Items, items));

        Assert.Contains("Needs attention", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("billing-api", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("client-mobile", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/repositories/billing-api\"", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/instances/client-mobile\"", panel.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("release-notes", panel.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NeedsAttentionPanel_Renders_Empty_State_When_No_Blocking_Items_Exist()
    {
        using BunitContext context = new();

        IRenderedComponent<NeedsAttentionPanel> panel = context.Render<NeedsAttentionPanel>(
            parameters => parameters.Add(
                component => component.Items,
                [Attention(
                    AlertSeverity.Info,
                    "release-notes",
                    "Daily digest is available",
                    "/reports/daily",
                    "report")]));

        Assert.Contains("All clear", panel.Markup, StringComparison.Ordinal);
        Assert.Contains("No critical or warning items are active.", panel.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("release-notes", panel.Markup, StringComparison.Ordinal);
    }

    private static void RegisterHomeServices(
        BunitContext context,
        DashboardProjection projection,
        ActiveRepositoryDashboard? activeRepositories = null)
    {
        context.Services.AddSingleton<IDashboardProjectionStore>(new StaticDashboardProjectionStore(projection));
        context.Services.AddSingleton<IActiveRepositoryDashboardQuery>(
            new StubActiveRepositoryDashboardQuery(activeRepositories ?? new ActiveRepositoryDashboard([])));
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

    private static void AssertMetricTile(IElement tile, string key, string label, string value)
    {
        Assert.Equal(key, tile.GetAttribute("data-dashboard-metric"));
        Assert.Equal($"{label} metric", tile.GetAttribute("aria-label"));
        Assert.Contains("metric-tile", tile.GetAttribute("class") ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(label, tile.TextContent, StringComparison.Ordinal);
        Assert.Contains(value, tile.TextContent, StringComparison.Ordinal);
        Assert.Contains("Sample detail", tile.TextContent, StringComparison.Ordinal);
        Assert.Contains("Within target", tile.TextContent, StringComparison.Ordinal);
    }

    private static void AssertStartupCheck(IElement check, string label, string route, string state)
    {
        Assert.Equal(label, check.QuerySelector("dt")?.TextContent);
        Assert.Equal(route, check.QuerySelector("code")?.TextContent);
        Assert.Equal(state, check.QuerySelector("span")?.TextContent);
    }

    private static DashboardAttentionItem Attention(
        AlertSeverity severity,
        string sourceName,
        string summary,
        string targetHref,
        string targetKind)
    {
        return new DashboardAttentionItem
        {
            Severity = severity,
            SourceName = sourceName,
            Summary = summary,
            TargetHref = targetHref,
            TargetKind = targetKind,
            CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T01:58:00Z"),
            AgeLabel = "2m ago"
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
