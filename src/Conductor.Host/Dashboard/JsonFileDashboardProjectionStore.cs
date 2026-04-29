using System.Text.Json;
using System.Text.Json.Serialization;
using Conductor.Core.Application.Dashboard;
using Microsoft.Extensions.Options;

namespace Conductor.Host.Dashboard;

public sealed class JsonFileDashboardProjectionStore(
    IOptions<DashboardProjectionOptions> options,
    IHostEnvironment environment) : IDashboardProjectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        string projectionPath = ResolvePath(options.Value.Path);

        await using FileStream stream = File.OpenRead(projectionPath);
        DashboardProjection? projection = await JsonSerializer.DeserializeAsync<DashboardProjection>(
            stream,
            JsonOptions,
            cancellationToken);

        return projection ?? DashboardProjection.Empty;
    }

    private string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }
}
