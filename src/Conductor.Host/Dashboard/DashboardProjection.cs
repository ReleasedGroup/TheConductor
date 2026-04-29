namespace Conductor.Host.Dashboard;

public sealed record DashboardProjection
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public string OperatorName { get; init; } = "Operator";

    public string DateScope { get; init; } = "Today";

    public string ProjectScope { get; init; } = "All Projects";

    public List<DashboardMetric> Metrics { get; init; } = [];

    public List<HealthHeatmapRow> HealthRows { get; init; } = [];

    public WorkloadProjection Workload { get; init; } = new();

    public List<AttentionItem> NeedsAttention { get; init; } = [];

    public List<RepositoryRow> Repositories { get; init; } = [];

    public List<ActivityEvent> Activity { get; init; } = [];

    public List<QuickAction> QuickActions { get; init; } = [];
}

public sealed record DashboardMetric
{
    public string Title { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string Trend { get; init; } = string.Empty;

    public string Tone { get; init; } = "neutral";

    public string Icon { get; init; } = string.Empty;
}

public sealed record HealthHeatmapRow
{
    public string Repository { get; init; } = string.Empty;

    public List<string> Buckets { get; init; } = [];
}

public sealed record WorkloadProjection
{
    public int TotalActiveIssues { get; init; }

    public List<WorkloadSlice> Slices { get; init; } = [];
}

public sealed record WorkloadSlice
{
    public string Label { get; init; } = string.Empty;

    public int Count { get; init; }

    public string Percentage { get; init; } = string.Empty;

    public string Color { get; init; } = "#60a5fa";
}

public sealed record AttentionItem
{
    public string Repository { get; init; } = string.Empty;

    public string Severity { get; init; } = "Warning";

    public string Summary { get; init; } = string.Empty;

    public string Age { get; init; } = string.Empty;
}

public sealed record RepositoryRow
{
    public string Project { get; init; } = string.Empty;

    public string Repository { get; init; } = string.Empty;

    public string Health { get; init; } = "Unknown";

    public int ActiveIssues { get; init; }

    public int RunningAgents { get; init; }

    public int FailedRuns { get; init; }

    public int OpenPullRequests { get; init; }

    public string LastActivity { get; init; } = string.Empty;

    public string Sparkline { get; init; } = string.Empty;
}

public sealed record ActivityEvent
{
    public string Time { get; init; } = string.Empty;

    public string Repository { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Tone { get; init; } = "neutral";
}

public sealed record QuickAction
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Href { get; init; } = "#";

    public string Icon { get; init; } = string.Empty;
}
