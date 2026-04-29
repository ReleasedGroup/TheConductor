namespace Conductor.Core.Application.Dashboard;

public sealed class WorkloadOverview
{
    private static readonly WorkloadStatus[] OrderedStatuses =
    [
        WorkloadStatus.New,
        WorkloadStatus.Queued,
        WorkloadStatus.Running,
        WorkloadStatus.Retrying,
        WorkloadStatus.PendingReview,
        WorkloadStatus.Blocked,
        WorkloadStatus.Failed,
        WorkloadStatus.Closed,
    ];

    private readonly IReadOnlyDictionary<WorkloadStatus, WorkloadStatusCount> countsByStatus;

    private WorkloadOverview(IReadOnlyList<WorkloadStatusCount> statusCounts)
    {
        StatusCounts = statusCounts;
        countsByStatus = statusCounts.ToDictionary(statusCount => statusCount.Status);
        TotalCount = statusCounts.Sum(statusCount => statusCount.Count);
        ActiveCount = statusCounts
            .Where(statusCount => statusCount.IsActive)
            .Sum(statusCount => statusCount.Count);
    }

    public static WorkloadOverview Empty { get; } = FromCounts([]);

    public IReadOnlyList<WorkloadStatusCount> StatusCounts { get; }

    public int TotalCount { get; }

    public int ActiveCount { get; }

    public bool HasActiveWorkload => ActiveCount > 0;

    public static WorkloadOverview FromCounts(IEnumerable<WorkloadStatusCount> statusCounts)
    {
        ArgumentNullException.ThrowIfNull(statusCounts);

        Dictionary<WorkloadStatus, int> counts = OrderedStatuses.ToDictionary(status => status, _ => 0);

        foreach (WorkloadStatusCount statusCount in statusCounts)
        {
            counts[statusCount.Status] += statusCount.Count;
        }

        return new WorkloadOverview(
            OrderedStatuses
                .Select(status => new WorkloadStatusCount(status, counts[status]))
                .ToArray());
    }

    public int CountFor(WorkloadStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A known workload status is required.");
        }

        return countsByStatus[status].Count;
    }
}
