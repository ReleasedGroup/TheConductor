namespace Conductor.Host.Components.Dashboard;

public sealed record HealthHeatmapBucket(
    string RepositoryName,
    string PeriodLabel,
    HealthHeatmapStatus Status,
    int Score,
    string Detail);
