namespace Conductor.Host.Dashboard;

public sealed record WorkloadProjection
{
    public int TotalActiveIssues { get; init; }

    public IReadOnlyList<WorkloadSlice> Slices { get; init; } = [];
}

public sealed record WorkloadSlice
{
    public string Label { get; init; } = string.Empty;

    public int Count { get; init; }

    public string Percentage { get; init; } = string.Empty;

    public string Color { get; init; } = "#60a5fa";
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
