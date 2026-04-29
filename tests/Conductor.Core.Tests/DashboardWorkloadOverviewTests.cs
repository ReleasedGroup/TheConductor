using Conductor.Core.Application.Dashboard;

namespace Conductor.Core.Tests;

public sealed class DashboardWorkloadOverviewTests
{
    [Fact]
    public void WorkloadOverview_FromCounts_Orders_All_Statuses_For_Dashboard_Rendering()
    {
        var overview = WorkloadOverview.FromCounts(
        [
            new WorkloadStatusCount(WorkloadStatus.Running, 3),
            new WorkloadStatusCount(WorkloadStatus.Blocked, 2),
            new WorkloadStatusCount(WorkloadStatus.Running, 1),
            new WorkloadStatusCount(WorkloadStatus.Closed, 5),
        ]);

        WorkloadStatus[] expectedOrder =
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

        Assert.Equal(expectedOrder, overview.StatusCounts.Select(statusCount => statusCount.Status));
        Assert.Equal(4, overview.CountFor(WorkloadStatus.Running));
        Assert.Equal(2, overview.CountFor(WorkloadStatus.Blocked));
        Assert.Equal(0, overview.CountFor(WorkloadStatus.Queued));
    }

    [Fact]
    public void WorkloadOverview_Computes_Active_Total_Without_Closed_Work()
    {
        var overview = WorkloadOverview.FromCounts(
        [
            new WorkloadStatusCount(WorkloadStatus.New, 2),
            new WorkloadStatusCount(WorkloadStatus.PendingReview, 1),
            new WorkloadStatusCount(WorkloadStatus.Closed, 7),
        ]);

        Assert.Equal(10, overview.TotalCount);
        Assert.Equal(3, overview.ActiveCount);
        Assert.True(overview.HasActiveWorkload);
    }

    [Fact]
    public void WorkloadOverview_Empty_Includes_Zero_Count_For_Each_Status()
    {
        var overview = WorkloadOverview.Empty;

        Assert.Equal(8, overview.StatusCounts.Count);
        Assert.Equal(0, overview.TotalCount);
        Assert.Equal(0, overview.ActiveCount);
        Assert.False(overview.HasActiveWorkload);
        Assert.All(overview.StatusCounts, statusCount => Assert.Equal(0, statusCount.Count));
    }

    [Fact]
    public void WorkloadStatusCount_Rejects_Negative_Counts()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkloadStatusCount(WorkloadStatus.Failed, -1));

        Assert.Equal("count", exception.ParamName);
    }
}
