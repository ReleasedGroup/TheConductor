namespace Conductor.Core.Application.Dashboard;

public interface IActiveRepositoryDashboardQuery
{
    Task<ActiveRepositoryDashboard> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record ActiveRepositoryDashboard(IReadOnlyList<ActiveRepositoryDashboardRow> Rows);

public sealed record ActiveRepositoryDashboardRow(
    string ProjectName,
    string RepositoryFullName,
    DashboardRepositoryHealth Health,
    int ActiveIssueCount,
    int RunningAgentCount,
    int FailedRunCount,
    int OpenPullRequestCount,
    DateTimeOffset? LastActivityAtUtc);

public enum DashboardRepositoryHealth
{
    Unknown,
    Healthy,
    Warning,
    Critical,
    Offline,
}
