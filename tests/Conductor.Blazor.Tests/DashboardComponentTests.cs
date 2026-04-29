using Bunit;
using Conductor.Host.Components.Dashboard;
using Conductor.Host.Components.Pages;
using Conductor.Host.Components.Shared;
using Conductor.Host.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class DashboardComponentTests : BunitContext
{
    [Fact]
    public void DashboardRendersProjectionData()
    {
        Services.AddSingleton<IDashboardProjectionStore>(
            new StubDashboardProjectionStore(CreateProjection()));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("Healthy Repos", cut.Markup));
        Assert.Contains("acme-portal", cut.Markup);
        Assert.Contains("Needs Attention", cut.Markup);
        Assert.Contains("Workspace prepared", cut.Markup);
    }

    [Fact]
    public void StatusBadgeIncludesTextAndToneClass()
    {
        var cut = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Critical"));

        var badge = cut.Find(".status-badge");

        Assert.Contains("Critical", badge.TextContent);
        Assert.Contains("status-critical", badge.GetAttribute("class"));
        Assert.Equal("Status: Critical", badge.GetAttribute("aria-label"));
    }

    [Fact]
    public void NeedsAttentionPanelRendersEmptyState()
    {
        var cut = Render<NeedsAttentionPanel>(parameters => parameters
            .Add(component => component.Items, Array.Empty<AttentionItem>()));

        Assert.Contains("No repositories need attention.", cut.Markup);
    }

    [Fact]
    public void RepositoryTableEmphasizesFailedRuns()
    {
        var repositories = new[]
        {
            new RepositoryRow
            {
                Project = "Billing Engine",
                Repository = "billing-api",
                Health = "Warning",
                ActiveIssues = 14,
                RunningAgents = 5,
                FailedRuns = 2,
                OpenPullRequests = 1,
                LastActivity = "1m ago",
                Sparkline = "_/\\_"
            }
        };

        var cut = Render<RepositoryTable>(parameters => parameters
            .Add(component => component.Repositories, repositories));

        Assert.Contains("billing-api", cut.Markup);
        Assert.NotNull(cut.Find("td.danger-cell"));
        Assert.Contains("status-warning", cut.Find(".status-badge").GetAttribute("class"));
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
        HealthRows =
        [
            new HealthHeatmapRow
            {
                Repository = "acme-portal",
                Buckets = ["Healthy", "Warning", "Critical", "Offline"]
            }
        ],
        Workload = new WorkloadProjection
        {
            TotalActiveIssues = 10,
            Slices =
            [
                new WorkloadSlice
                {
                    Label = "In Progress",
                    Count = 10,
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
                Summary = "2 failed runs in the last 30 minutes",
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
