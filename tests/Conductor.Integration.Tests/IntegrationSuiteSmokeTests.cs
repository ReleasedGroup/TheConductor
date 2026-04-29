namespace Conductor.Integration.Tests;

public sealed class IntegrationSuiteSmokeTests
{
    [Fact]
    public void Integration_Suite_Is_Wired_For_OptIn_External_Tests()
    {
        Assert.Contains(
            "Conductor.Integration.Tests",
            typeof(IntegrationSuiteSmokeTests).Assembly.FullName,
            StringComparison.Ordinal);
    }
}
