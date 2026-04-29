using AngleSharp.Dom;
using Bunit;
using Conductor.Host.Components.Pages;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Initial_Dashboard()
    {
        using BunitContext context = new();

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        Assert.Contains("Conductor Dashboard", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("SQLite persistence registration", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Repository orchestration health", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Release Portal", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Startup verification", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("/health/live", dashboard.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_Renders_Dashboard_Metric_Tiles_With_Baseline_States()
    {
        using BunitContext context = new();

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        IElement metricGrid = dashboard.Find("section[aria-label='Dashboard metrics']");
        IReadOnlyList<IElement> tiles = dashboard.FindAll(".metric-tile");

        Assert.Equal("Dashboard metrics", metricGrid.GetAttribute("aria-label"));
        Assert.Collection(
            tiles,
            tile => AssertMetricTile(tile, "Projects", "0", "Ready for Sprint 1 seed data"),
            tile => AssertMetricTile(tile, "Repositories", "0", "Import workflow pending"),
            tile => AssertMetricTile(tile, "Instances", "0", "Provisioning starts in later sprints"),
            tile => AssertMetricTile(tile, "Alerts", "0", "In-app rules not active yet"));
    }

    [Fact]
    public void Home_Renders_Startup_Verification_Cards()
    {
        using BunitContext context = new();

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        IReadOnlyList<IElement> checks = dashboard.FindAll(".startup-panel dl > div");

        Assert.Collection(
            checks,
            check => AssertStartupCheck(check, "Dashboard route", "/", "Dashboard served"),
            check => AssertStartupCheck(check, "Live health", "/health/live", "Probe available"),
            check => AssertStartupCheck(check, "Ready health", "/health/ready", "Probe available"));
    }

    private static void AssertMetricTile(IElement tile, string label, string value, string hint)
    {
        Assert.Equal(label, tile.QuerySelector("span")?.TextContent);
        Assert.Equal(value, tile.QuerySelector("strong")?.TextContent);
        Assert.Equal(hint, tile.QuerySelector("small")?.TextContent);
    }

    private static void AssertStartupCheck(IElement check, string label, string route, string state)
    {
        Assert.Equal(label, check.QuerySelector("dt")?.TextContent);
        Assert.Equal(route, check.QuerySelector("code")?.TextContent);
        Assert.Equal(state, check.QuerySelector("span")?.TextContent);
    }
}
