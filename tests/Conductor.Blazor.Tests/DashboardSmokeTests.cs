using Bunit;
using Conductor.Host.Components.Pages;

namespace Conductor.Blazor.Tests;

public sealed class DashboardSmokeTests
{
    [Fact]
    public void Home_Renders_Initial_Dashboard()
    {
        using BunitContext context = new();

        IRenderedComponent<Home> dashboard = context.Render<Home>();

        Assert.Contains("Conductor Dashboard", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("SQLite persistence registration", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Repository orchestration health", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Release Portal", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("Startup verification", dashboard.Markup, StringComparison.Ordinal);
        Assert.Contains("/health/live", dashboard.Markup, StringComparison.Ordinal);
    }
}
