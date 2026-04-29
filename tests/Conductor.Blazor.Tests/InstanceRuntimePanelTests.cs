using AngleSharp.Dom;
using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Host.Components.Dashboard;

namespace Conductor.Blazor.Tests;

public sealed class InstanceRuntimePanelTests
{
    [Fact]
    public void InstanceRuntimePanel_Renders_Status_Workflow_Version_And_Poll_Data()
    {
        using BunitContext context = new();
        DashboardInstanceRuntime[] instances =
        [
            Instance(
                "api-primary",
                "API primary",
                InstanceHealthStatus.Healthy,
                InstanceLifecycleStatus.Running),
            Instance(
                "mobile-offline",
                "Mobile offline",
                InstanceHealthStatus.Offline,
                InstanceLifecycleStatus.Failed)
        ];

        IRenderedComponent<InstanceRuntimePanel> component = context.Render<InstanceRuntimePanel>(
            parameters => parameters.Add(panel => panel.Items, instances));

        Assert.Contains("Instance health", component.Markup, StringComparison.Ordinal);
        Assert.Contains("API primary", component.Markup, StringComparison.Ordinal);
        Assert.Contains("ReleasedGroup/api-service", component.Markup, StringComparison.Ordinal);
        Assert.Contains("http://localhost:5010/", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Healthy", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Running", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1.2.3", component.Markup, StringComparison.Ordinal);
        Assert.Contains("ReleasedGroup/api-service", component.Markup, StringComparison.Ordinal);
        Assert.Contains("/config/api-service/WORKFLOW.md", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Apr 29, 01:58 UTC", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Snapshot", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Active", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Retry", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Failed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Tokens", component.Markup, StringComparison.Ordinal);
        Assert.Contains("140", component.Markup, StringComparison.Ordinal);

        IReadOnlyList<IElement> rows = component.FindAll("[data-instance-runtime]");
        Assert.Equal("mobile-offline", rows[0].GetAttribute("data-instance-runtime"));
        Assert.Contains("instance-runtime-row--offline", rows[0].GetAttribute("class") ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("status-badge--neutral", rows[0].InnerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void InstanceRuntimePanel_Renders_Empty_State_Without_Runtime_Data()
    {
        using BunitContext context = new();

        IRenderedComponent<InstanceRuntimePanel> component = context.Render<InstanceRuntimePanel>();

        Assert.Contains("No Symphony runtime data is available yet.", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void InstanceRuntimePanel_Falls_Back_For_Missing_Runtime_Metadata()
    {
        using BunitContext context = new();
        DashboardInstanceRuntime[] instances =
        [
            new()
            {
                Key = "unknown",
                DisplayName = "Unknown runtime",
                HealthStatus = InstanceHealthStatus.Unknown,
                LifecycleStatus = InstanceLifecycleStatus.Provisioned
            }
        ];

        IRenderedComponent<InstanceRuntimePanel> component = context.Render<InstanceRuntimePanel>(
            parameters => parameters.Add(panel => panel.Items, instances));

        Assert.Contains("Version unknown", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Workflow unknown", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Workflow path unknown", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Not polled", component.Markup, StringComparison.Ordinal);
        Assert.Contains("status-badge--info", component.Markup, StringComparison.Ordinal);
    }

    private static DashboardInstanceRuntime Instance(
        string key,
        string displayName,
        InstanceHealthStatus healthStatus,
        InstanceLifecycleStatus lifecycleStatus)
    {
        return new DashboardInstanceRuntime
        {
            Key = key,
            DisplayName = displayName,
            RepositoryFullName = "ReleasedGroup/api-service",
            BaseUrl = new Uri("http://localhost:5010/"),
            HealthStatus = healthStatus,
            LifecycleStatus = lifecycleStatus,
            SymphonyVersion = "1.2.3",
            WorkflowOwner = "ReleasedGroup",
            WorkflowRepository = "api-service",
            WorkflowSourcePath = "/config/api-service/WORKFLOW.md",
            LastHealthCheckAtUtc = DateTimeOffset.Parse("2026-04-29T01:58:00Z"),
            LastSnapshotCapturedAtUtc = DateTimeOffset.Parse("2026-04-29T01:57:30Z"),
            LastSeenAtUtc = DateTimeOffset.Parse("2026-04-29T01:58:00Z"),
            ActiveIssueCount = 4,
            RunningSessionCount = 2,
            RetryQueueCount = 1,
            FailedRunCount = 0,
            TokenTotal = 140
        };
    }
}
