using Conductor.Core.Application.Dashboard;
using Conductor.Host.Dashboard;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Conductor.Blazor.Tests.Dashboard;

public sealed class JsonFileDashboardProjectionStoreTests
{
    [Fact]
    public async Task StoreReadsDashboardProjectionFromJsonFile()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                tempPath,
                """
                {
                  "capturedAtUtc": "2026-04-29T00:00:00Z",
                  "metrics": [
                    {
                      "key": "healthy-repositories",
                      "label": "Healthy Repos",
                      "value": "36 / 42",
                      "trendDirection": "Positive",
                      "tone": "Healthy",
                      "icon": "pulse"
                    }
                  ]
                }
                """);

            var store = new JsonFileDashboardProjectionStore(
                Options.Create(new DashboardProjectionOptions { Path = tempPath }),
                new TestHostEnvironment());

            DashboardProjection projection = await store.GetCurrentAsync();

            Assert.Equal(DateTimeOffset.Parse("2026-04-29T00:00:00Z"), projection.CapturedAtUtc);
            DashboardMetric metric = Assert.Single(projection.Metrics);
            Assert.Equal("healthy-repositories", metric.Key);
            Assert.Equal(MetricTrendDirection.Positive, metric.TrendDirection);
            Assert.Equal(MetricTone.Healthy, metric.Tone);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Conductor.Blazor.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
