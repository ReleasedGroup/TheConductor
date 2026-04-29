namespace Conductor.Core.Application.Dashboard;

public enum WorkloadStatus
{
    New,
    Queued,
    Running,
    Retrying,
    PendingReview,
    Blocked,
    Failed,
    Closed,
}
