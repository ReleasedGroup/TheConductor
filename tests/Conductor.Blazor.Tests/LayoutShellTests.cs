using Bunit;
using Conductor.Host.Components.Layout;

namespace Conductor.Blazor.Tests;

public sealed class LayoutShellTests
{
    [Fact]
    public void MainLayout_Renders_AppShell_Navigation_TopBar_And_Body()
    {
        using BunitContext context = new();

        IRenderedComponent<MainLayout> layout = context.Render<MainLayout>(
            parameters => parameters.Add(
                component => component.Body,
                builder => builder.AddMarkupContent(0, "<h1>Dashboard body</h1>")));

        Assert.NotNull(layout.Find(".app-shell"));
        Assert.Contains("Software Delivery Control Surface", layout.Markup, StringComparison.Ordinal);
        Assert.Contains("Search repos, projects, issues", layout.Markup, StringComparison.Ordinal);
        Assert.Contains("Dashboard body", layout.Markup, StringComparison.Ordinal);
    }
}
