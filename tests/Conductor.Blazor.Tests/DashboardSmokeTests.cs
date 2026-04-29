using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Initial_Dashboard()
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
            ]
        }));

        IRenderedComponent<Home> dashboard = context.Render<Home>();

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
        Assert.Contains("Startup verification", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("/health/live", dashboard.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_Renders_Empty_State_When_No_Metrics_Exist()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IDashboardProjectionStore>(
            new StaticDashboardProjectionStore(DashboardProjection.Empty));

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
}
