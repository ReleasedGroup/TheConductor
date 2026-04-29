namespace Conductor.Core.Application.Dashboard;

public sealed class DashboardProjection
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;

    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = Array.Empty<DashboardMetric>();

    public static DashboardProjection Empty { get; } = new();
}
