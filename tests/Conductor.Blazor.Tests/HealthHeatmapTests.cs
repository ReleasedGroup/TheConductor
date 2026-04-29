using Bunit;
using Conductor.Host.Components.Dashboard;

namespace Conductor.Blazor.Tests;

public sealed class HealthHeatmapTests
{
    [Fact]
    public void HealthHeatmap_Renders_Buckets_Across_Periods()
    {
        using BunitContext context = new();
        HealthHeatmapBucket[] buckets =
        [
            new("Billing API", "Mon", HealthHeatmapStatus.Healthy, 99, "All jobs completed."),
            new("Billing API", "Tue", HealthHeatmapStatus.Warning, 74, "Retry queue elevated."),
            new("Portal", "Mon", HealthHeatmapStatus.Critical, 38, "Deployment blocked."),
        ];

        IRenderedComponent<HealthHeatmap> component = context.Render<HealthHeatmap>(parameters => parameters
            .Add(component => component.Title, "Repository orchestration health")
            .Add(component => component.Description, "Last 48 hours")
            .Add(component => component.Buckets, buckets));

        Assert.Contains("Repository orchestration health", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Last 48 hours", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Billing API", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Portal", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Mon", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Tue", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Warn", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Crit", component.Markup, StringComparison.Ordinal);
        Assert.Contains(
            "Billing API Tue: Warning with health score 74. Retry queue elevated.",
            component.Markup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HealthHeatmap_Renders_Empty_State_Without_Buckets()
    {
        using BunitContext context = new();

        IRenderedComponent<HealthHeatmap> component = context.Render<HealthHeatmap>();

        Assert.Contains(
            "No repository health buckets are available yet.",
            component.Markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<table", component.Markup, StringComparison.Ordinal);
    }
}
