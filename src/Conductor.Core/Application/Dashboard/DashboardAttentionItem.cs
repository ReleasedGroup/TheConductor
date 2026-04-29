using Conductor.Core.Domain;

namespace Conductor.Core.Application.Dashboard;

public sealed class DashboardAttentionItem
{
    public AlertSeverity Severity { get; init; } = AlertSeverity.Warning;

    public string SourceName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string TargetHref { get; init; } = "#";

    public string TargetKind { get; init; } = "source";

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;

    public string AgeLabel { get; init; } = string.Empty;
}
