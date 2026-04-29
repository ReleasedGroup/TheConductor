namespace Conductor.Core.Application.Dashboard;

public sealed class DashboardProjection
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;

    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = Array.Empty<DashboardMetric>();

    public IReadOnlyList<DashboardAttentionItem> AttentionItems { get; init; } = Array.Empty<DashboardAttentionItem>();

    public IReadOnlyList<DashboardInstanceRuntime> InstanceRuntimes { get; init; } = Array.Empty<DashboardInstanceRuntime>();

    public static DashboardProjection Empty { get; } = new();
}
