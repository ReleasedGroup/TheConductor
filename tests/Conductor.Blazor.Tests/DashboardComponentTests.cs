using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
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
            new StaticDashboardProjectionStore(CreateProjection()));

        IRenderedComponent<Home> dashboard = Render<Home>();

        dashboard.WaitForAssertion(() => Assert.Contains("Healthy Repos", dashboard.Markup));
        Assert.Contains("acme-portal", dashboard.Markup);
        Assert.Contains("Needs attention", dashboard.Markup);
        Assert.Contains("billing-api", dashboard.Markup);
        Assert.Contains("Workspace prepared", dashboard.Markup);
    }

    [Fact]
    public void StatusBadgeIncludesTextAndToneClass()
    {
        IRenderedComponent<StatusBadge> badge = Render<StatusBadge>(parameters => parameters
            .Add(component => component.Text, "Critical")
            .Add(component => component.Tone, StatusBadgeTone.Critical));

        var element = badge.Find(".status-badge");

        Assert.Contains("Critical", element.TextContent);
        Assert.Contains("status-badge--critical", element.GetAttribute("class"));
        Assert.Equal("Critical status: Critical", element.GetAttribute("aria-label"));
    }

    [Fact]
    public void NeedsAttentionPanelRendersEmptyState()
    {
        IRenderedComponent<NeedsAttentionPanel> panel = Render<NeedsAttentionPanel>(parameters => parameters
            .Add(component => component.Items, Array.Empty<DashboardAttentionItem>()));

        Assert.Contains("All clear", panel.Markup);
        Assert.Contains("No critical or warning items are active.", panel.Markup);
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

        IRenderedComponent<RepositoryTable> table = Render<RepositoryTable>(parameters => parameters
            .Add(component => component.Repositories, repositories));

        Assert.Contains("billing-api", table.Markup);
        Assert.NotNull(table.Find("td.danger-cell"));
        Assert.Contains("status-badge--warning", table.Find(".status-badge").GetAttribute("class"));
    }

    private static DashboardProjection CreateProjection()
    {
        return new DashboardProjection
        {
            CapturedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
            Metrics =
            [
                new()
                {
                    Key = "healthy-repositories",
                    Label = "Healthy Repos",
                    Value = "36 / 42",
                    Detail = "85% healthy",
                    TrendText = "Up 8%",
                    TrendDirection = MetricTrendDirection.Positive,
                    Tone = MetricTone.Healthy,
                    Icon = "pulse"
                }
            ],
            AttentionItems =
            [
                new()
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
