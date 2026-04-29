using Bunit;
using Conductor.Host.Components.Dashboard;
using Conductor.Host.Components.Pages;
using Conductor.Host.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Initial_Dashboard()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IDashboardProjectionStore>(
            new StubDashboardProjectionStore(CreateProjection()));

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        Assert.Contains("Dashboard", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Healthy Repos", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Orchestration Health", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Needs Attention", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Active Repositories", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Live Activity", dashboard.Markup, StringComparison.Ordinal);
    }

    private static DashboardProjection CreateProjection() => new()
    {
        OperatorName = "Nick",
        DateScope = "Apr 29, 2026",
        ProjectScope = "All Projects",
        Metrics =
        [
            new DashboardMetric
            {
                Title = "Healthy Repos",
                Value = "36 / 42",
                Detail = "85% healthy",
                Trend = "+8%",
                Tone = "healthy",
                Icon = "pulse"
            }
        ],
        HealthBuckets =
        [
            new("acme-portal", "Now", HealthHeatmapStatus.Healthy, 99, "All checks completed.")
        ],
        Workload = new WorkloadProjection
        {
            TotalActiveIssues = 1,
            Slices =
            [
                new WorkloadSlice
                {
                    Label = "In Progress",
                    Count = 1,
                    Percentage = "100%",
                    Color = "#3b82f6"
                }
            ]
        },
        NeedsAttention =
        [
            new AttentionItem
            {
                Repository = "billing-api",
                Severity = "Critical",
                Summary = "2 failed runs",
                Age = "2m ago"
            }
        ],
        Repositories =
        [
            new RepositoryRow
            {
                Project = "ACME Portal",
                Repository = "acme-portal",
                Health = "Healthy",
                ActiveIssues = 9,
                RunningAgents = 3,
                FailedRuns = 0,
                OpenPullRequests = 2,
                LastActivity = "4m ago",
                Sparkline = "__/\\_"
            }
        ],
        Activity =
        [
            new ActivityEvent
            {
                Time = "09:14",
                Repository = "acme-portal",
                Reference = "#128",
                Summary = "Workspace prepared",
                Tone = "healthy"
            }
        ],
        QuickActions =
        [
            new QuickAction
            {
                Title = "Import Repository",
                Description = "Add a GitHub repository to orchestration",
                Href = "/repositories",
                Icon = "cloud"
            }
        ]
    };

    private sealed class StubDashboardProjectionStore(DashboardProjection projection) : IDashboardProjectionStore
    {
        public ValueTask<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(projection);
    }
}
