using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Host.Components.Dashboard;

namespace Conductor.Blazor.Tests.Dashboard;

public sealed class MetricTileTests
{
    [Fact]
    public void MetricTileRendersCoreMetricText()
    {
        using BunitContext context = new();
        var metric = new DashboardMetric
        {
            Key = "active-agents",
            Label = "Active Agents",
            Value = "18",
            Detail = "Running now",
            TrendText = "Up 3 from yesterday",
            TrendDirection = MetricTrendDirection.Positive,
            Tone = MetricTone.Active,
            Icon = "agents"
        };

        IRenderedComponent<MetricTile> tile = context.Render<MetricTile>(parameters => parameters
            .Add(component => component.Metric, metric));

        var article = tile.Find("[data-dashboard-metric='active-agents']");
        Assert.Contains("Active Agents", article.TextContent, StringComparison.Ordinal);
        Assert.Contains("18", article.TextContent, StringComparison.Ordinal);
        Assert.Contains("Running now", article.TextContent, StringComparison.Ordinal);
        Assert.Contains("Up 3 from yesterday", article.TextContent, StringComparison.Ordinal);
        Assert.Contains("metric-tile--active", article.ClassList);
    }

    [Fact]
    public void MetricTileIncludesAccessibleTrendLabel()
    {
        using BunitContext context = new();
        var metric = new DashboardMetric
        {
            Key = "blocked-issues",
            Label = "Blocked Issues",
            Value = "7",
            TrendText = "Up 2 from yesterday",
            TrendDirection = MetricTrendDirection.Negative,
            Tone = MetricTone.Warning,
            Icon = "warning"
        };

        IRenderedComponent<MetricTile> tile = context.Render<MetricTile>(parameters => parameters
            .Add(component => component.Metric, metric));

        Assert.Equal("Trend:", tile.Find(".sr-only").TextContent);
        Assert.Contains("metric-tile__trend--negative", tile.Find(".metric-tile__trend").ClassList);
    }
}
