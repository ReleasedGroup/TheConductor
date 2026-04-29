using AngleSharp.Dom;
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

    [Fact]
    public void HealthHeatmap_Renders_Status_Summary_For_All_Visual_States()
    {
        using BunitContext context = new();
        HealthHeatmapBucket[] buckets =
        [
            new("API", "09:00", HealthHeatmapStatus.Healthy, 99, "Current."),
            new("API", "10:00", HealthHeatmapStatus.Warning, 72, "Retry queue elevated."),
            new("Worker", "09:00", HealthHeatmapStatus.Critical, 33, "Failed runs."),
            new("Docs", "09:00", HealthHeatmapStatus.Offline, 0, "No heartbeat."),
            new("Docs", "10:00", HealthHeatmapStatus.Healthy, 98, "Recovered."),
        ];

        IRenderedComponent<HealthHeatmap> component = context.Render<HealthHeatmap>(parameters => parameters
            .Add(component => component.Buckets, buckets));

        IReadOnlyList<IElement> summaries = component.FindAll(".heatmap-summary .status-badge");

        Assert.Collection(
            summaries,
            summary => AssertSummaryBadge(summary, "Healthy 2", "status-badge--success"),
            summary => AssertSummaryBadge(summary, "Warning 1", "status-badge--warning"),
            summary => AssertSummaryBadge(summary, "Critical 1", "status-badge--critical"),
            summary => AssertSummaryBadge(summary, "Offline 1", "status-badge--neutral"));
    }

    [Fact]
    public void HealthHeatmap_Applies_Status_Classes_Labels_And_Clamped_Scores()
    {
        using BunitContext context = new();
        HealthHeatmapBucket[] buckets =
        [
            new("Healthy Repo", "Now", HealthHeatmapStatus.Healthy, 110, string.Empty),
            new("Warning Repo", "Now", HealthHeatmapStatus.Warning, -7, "Retry queue elevated."),
            new("Critical Repo", "Now", HealthHeatmapStatus.Critical, 38, "Deployment blocked."),
            new("Offline Repo", "Now", HealthHeatmapStatus.Offline, 0, "No heartbeat received."),
        ];

        IRenderedComponent<HealthHeatmap> component = context.Render<HealthHeatmap>(parameters => parameters
            .Add(component => component.Buckets, buckets));

        AssertHeatmapCell(
            component,
            "Healthy Repo Now: Healthy with health score 100. No detail recorded.",
            "heatmap-cell--healthy",
            "OK",
            "100");
        AssertHeatmapCell(
            component,
            "Warning Repo Now: Warning with health score 0. Retry queue elevated.",
            "heatmap-cell--warning",
            "Warn",
            "0");
        AssertHeatmapCell(
            component,
            "Critical Repo Now: Critical with health score 38. Deployment blocked.",
            "heatmap-cell--critical",
            "Crit",
            "38");
        AssertHeatmapCell(
            component,
            "Offline Repo Now: Offline with health score 0. No heartbeat received.",
            "heatmap-cell--offline",
            "Off",
            "0");
    }

    [Fact]
    public void HealthHeatmap_Renders_Empty_Cells_For_Missing_Periods()
    {
        using BunitContext context = new();
        HealthHeatmapBucket[] buckets =
        [
            new("Billing API", "Mon", HealthHeatmapStatus.Healthy, 99, "All jobs completed."),
            new("Billing API", "Tue", HealthHeatmapStatus.Warning, 74, "Retry queue elevated."),
            new("Portal", "Mon", HealthHeatmapStatus.Critical, 38, "Deployment blocked."),
        ];

        IRenderedComponent<HealthHeatmap> component = context.Render<HealthHeatmap>(parameters => parameters
            .Add(component => component.Buckets, buckets));

        IElement emptyCell = FindCellByAriaLabel(component, "Portal Tue: No data");

        AssertClassContains(emptyCell, "heatmap-cell-empty");
        Assert.Equal("None", emptyCell.QuerySelector(".heatmap-cell-status")?.TextContent);
        Assert.Equal("-", emptyCell.QuerySelector(".heatmap-cell-score")?.TextContent);
    }

    [Fact]
    public void HealthHeatmap_Ignores_Buckets_Without_Row_Or_Period_Labels()
    {
        using BunitContext context = new();
        HealthHeatmapBucket[] buckets =
        [
            new("Valid Repo", "Mon", HealthHeatmapStatus.Healthy, 99, "Included."),
            new("", "Mon", HealthHeatmapStatus.Warning, 70, "Missing repository."),
            new("Missing Period", "", HealthHeatmapStatus.Critical, 30, "Missing period."),
            new("   ", "Tue", HealthHeatmapStatus.Offline, 0, "Blank repository."),
        ];

        IRenderedComponent<HealthHeatmap> component = context.Render<HealthHeatmap>(parameters => parameters
            .Add(component => component.Buckets, buckets));

        Assert.Contains("Valid Repo", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing Period", component.Markup, StringComparison.Ordinal);

        IReadOnlyList<IElement> rows = component.FindAll("tbody tr");
        IReadOnlyList<IElement> periods = component.FindAll("thead th:not(:first-child)");

        Assert.Single(rows);
        Assert.Single(periods);
        Assert.Equal("Mon", periods[0].TextContent);
    }

    private static void AssertSummaryBadge(IElement summary, string label, string expectedClass)
    {
        Assert.Equal(label, summary.TextContent.Trim());
        AssertClassContains(summary, expectedClass);
    }

    private static void AssertHeatmapCell(
        IRenderedComponent<HealthHeatmap> component,
        string ariaLabel,
        string expectedClass,
        string shortLabel,
        string score)
    {
        IElement cell = FindCellByAriaLabel(component, ariaLabel);

        AssertClassContains(cell, expectedClass);
        Assert.Equal(shortLabel, cell.QuerySelector(".heatmap-cell-status")?.TextContent);
        Assert.Equal(score, cell.QuerySelector(".heatmap-cell-score")?.TextContent);
    }

    private static IElement FindCellByAriaLabel(IRenderedComponent<HealthHeatmap> component, string ariaLabel) =>
        component.FindAll(".heatmap-cell")
            .Single(cell => string.Equals(cell.GetAttribute("aria-label"), ariaLabel, StringComparison.Ordinal));

    private static void AssertClassContains(IElement? element, string expectedClass)
    {
        Assert.NotNull(element);
        Assert.Contains(expectedClass, element.GetAttribute("class") ?? string.Empty, StringComparison.Ordinal);
    }
}
