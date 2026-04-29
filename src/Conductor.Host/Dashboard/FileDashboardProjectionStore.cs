using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Conductor.Host.Dashboard;

public interface IDashboardProjectionStore
{
    ValueTask<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed class FileDashboardProjectionStore(
    IWebHostEnvironment environment,
    IOptions<DashboardProjectionOptions> options,
    ILogger<FileDashboardProjectionStore> logger) : IDashboardProjectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var projectionPath = ResolveProjectionPath();

        if (!File.Exists(projectionPath))
        {
            throw new InvalidOperationException($"Dashboard projection file was not found at '{projectionPath}'.");
        }

        try
        {
            await using var stream = File.OpenRead(projectionPath);
            var projection = await JsonSerializer.DeserializeAsync<DashboardProjection>(
                stream,
                JsonOptions,
                cancellationToken);

            return projection ?? throw new InvalidOperationException("Dashboard projection file did not contain data.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Dashboard projection file could not be parsed.");
            throw new InvalidOperationException("Dashboard projection file could not be parsed.", ex);
        }
    }

    private string ResolveProjectionPath()
    {
        var configuredPath = options.Value.DashboardProjectionPath;

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }
}
