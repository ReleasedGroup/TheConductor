namespace Conductor.Core.Application.Dashboard;

public sealed record WorkloadStatusCount
{
    public WorkloadStatusCount(WorkloadStatus status, int count)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A known workload status is required.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Workload counts cannot be negative.");
        }

        Status = status;
        Count = count;
    }

    public WorkloadStatus Status { get; }

    public int Count { get; }

    public bool IsActive => Status is not WorkloadStatus.Closed;

    public string Label => Status switch
    {
        WorkloadStatus.New => "New",
        WorkloadStatus.Queued => "Queued",
        WorkloadStatus.Running => "Running",
        WorkloadStatus.Retrying => "Retrying",
        WorkloadStatus.PendingReview => "Pending review",
        WorkloadStatus.Blocked => "Blocked",
        WorkloadStatus.Failed => "Failed",
        WorkloadStatus.Closed => "Closed",
        _ => throw new InvalidOperationException($"Unsupported workload status '{Status}'."),
    };
}
