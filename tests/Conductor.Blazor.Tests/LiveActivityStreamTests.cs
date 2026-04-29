using Bunit;
using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Host.Components.Dashboard;

namespace Conductor.Blazor.Tests;

public sealed class LiveActivityStreamTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 4, 29, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LiveActivityStream_Renders_Recent_Events_In_Descending_Time_Order()
    {
        using BunitContext context = new();
        LiveActivityEvent[] events =
        [
            new(ReferenceTime.AddMinutes(-10), EventSeverity.Warning, "Instance offline", "client-mobile", null, "Symphony instance stopped responding."),
            new(ReferenceTime.AddMinutes(-2), EventSeverity.Error, "Run failed", "billing-api", 77, "Tests failed and a continuation started."),
            new(ReferenceTime.AddMinutes(-20), EventSeverity.Information, "Workspace prepared", "acme-portal", 128, "Workspace prepared for execution."),
        ];

        IRenderedComponent<LiveActivityStream> component = context.Render<LiveActivityStream>(parameters => parameters
            .Add(component => component.Events, events)
            .Add(component => component.ReferenceTimeUtc, ReferenceTime));

        string markup = component.Markup;

        Assert.Contains("Live activity", markup, StringComparison.Ordinal);
        Assert.Contains("2m ago", markup, StringComparison.Ordinal);
        Assert.Contains("10m ago", markup, StringComparison.Ordinal);
        Assert.Contains("20m ago", markup, StringComparison.Ordinal);
        Assert.Contains("billing-api", markup, StringComparison.Ordinal);
        Assert.Contains("#77", markup, StringComparison.Ordinal);
        Assert.Contains("Error", markup, StringComparison.Ordinal);
        Assert.True(
            markup.IndexOf("billing-api", StringComparison.Ordinal) < markup.IndexOf("client-mobile", StringComparison.Ordinal),
            "Expected newest activity to render before older activity.");
    }

    [Fact]
    public void LiveActivityStream_Renders_Empty_State_Without_Events()
    {
        using BunitContext context = new();

        IRenderedComponent<LiveActivityStream> component = context.Render<LiveActivityStream>();

        Assert.Contains("No operational activity has been recorded yet.", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<ol", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveActivityEvent_Requires_Positive_Issue_Number()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LiveActivityEvent(ReferenceTime, EventSeverity.Information, "Run queued", "billing-api", 0, "Queued."));

        Assert.Equal("issueNumber", exception.ParamName);
    }
}
