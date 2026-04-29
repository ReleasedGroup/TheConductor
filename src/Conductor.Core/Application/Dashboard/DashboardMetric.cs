namespace Conductor.Core.Application.Dashboard;

public sealed class DashboardMetric
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Value { get; init; }

    public string? Detail { get; init; }

    public string? TrendText { get; init; }

    public MetricTrendDirection TrendDirection { get; init; } = MetricTrendDirection.Neutral;

    public MetricTone Tone { get; init; } = MetricTone.Neutral;

    public required string Icon { get; init; }
}
