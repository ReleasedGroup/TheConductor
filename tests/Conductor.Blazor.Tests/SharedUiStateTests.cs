using Bunit;
using Conductor.Host.Components.Shared;

namespace Conductor.Blazor.Tests;

public sealed class SharedUiStateTests
{
    [Fact]
    public void StatusBadge_Renders_Text_Tone_Class_And_Accessible_Label()
    {
        using BunitContext context = new();

        IRenderedComponent<StatusBadge> component = context.Render<StatusBadge>(parameters => parameters
            .Add(badge => badge.Text, "Healthy")
            .Add(badge => badge.Tone, StatusBadgeTone.Success));

        Assert.Contains("Healthy", component.Markup, StringComparison.Ordinal);
        Assert.Contains("status-badge--success", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Success status: Healthy", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusBadge_Uses_Tone_Label_When_Text_Is_Empty()
    {
        using BunitContext context = new();

        IRenderedComponent<StatusBadge> component = context.Render<StatusBadge>(parameters => parameters
            .Add(badge => badge.Tone, StatusBadgeTone.Warning));

        Assert.Contains(">Warning<", component.Markup, StringComparison.Ordinal);
        Assert.Contains("status-badge--warning", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingState_Renders_Polite_Busy_Status()
    {
        using BunitContext context = new();

        IRenderedComponent<LoadingState> component = context.Render<LoadingState>(parameters => parameters
            .Add(state => state.Title, "Loading repositories")
            .Add(state => state.Message, "Fetching dashboard projection data."));

        Assert.Contains("role=\"status\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("aria-busy=\"true\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Loading repositories", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Fetching dashboard projection data.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyState_Renders_Action_Content()
    {
        using BunitContext context = new();

        IRenderedComponent<EmptyState> component = context.Render<EmptyState>(parameters => parameters
            .Add(state => state.Title, "No repositories yet")
            .Add(state => state.Message, "Imported repositories will appear here.")
            .AddChildContent("<button type=\"button\">Import repository</button>"));

        Assert.Contains("No repositories yet", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Imported repositories will appear here.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Import repository", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorState_Renders_Alert_Detail_And_Action_Content()
    {
        using BunitContext context = new();

        IRenderedComponent<ErrorState> component = context.Render<ErrorState>(parameters => parameters
            .Add(state => state.Title, "Dashboard failed")
            .Add(state => state.Message, "Projection data could not be loaded.")
            .Add(state => state.Detail, "Correlation ID: abc123")
            .AddChildContent("<button type=\"button\">Retry</button>"));

        Assert.Contains("role=\"alert\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Dashboard failed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Projection data could not be loaded.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Correlation ID: abc123", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Retry", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessState_Renders_Polite_Status_And_Action_Content()
    {
        using BunitContext context = new();

        IRenderedComponent<SuccessState> component = context.Render<SuccessState>(parameters => parameters
            .Add(state => state.Title, "Repository imported")
            .Add(state => state.Message, "The repository is ready for orchestration.")
            .AddChildContent("<a href=\"/repositories\">View repositories</a>"));

        Assert.Contains("role=\"status\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Repository imported", component.Markup, StringComparison.Ordinal);
        Assert.Contains("The repository is ready for orchestration.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("View repositories", component.Markup, StringComparison.Ordinal);
    }
}
