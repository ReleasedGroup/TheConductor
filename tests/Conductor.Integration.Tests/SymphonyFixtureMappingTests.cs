using Conductor.Core.Domain;
using Conductor.Infrastructure.Symphony;

namespace Conductor.Integration.Tests;

public sealed class SymphonyFixtureMappingTests
{
    [Fact]
    public void Health_Fixture_Maps_To_Healthy_Response()
    {
        var response = SymphonyResponseMapper.MapHealth(
            ReadFixture("health-ok.json"),
            httpStatusCode: 200,
            TimeSpan.FromMilliseconds(42));

        Assert.Equal(InstanceHealthStatus.Healthy, response.Status);
        Assert.Equal(200, response.HttpStatusCode);
        Assert.Equal(TimeSpan.FromMilliseconds(42), response.Latency);
        Assert.Equal("Healthy", response.RawStatus);
        Assert.Contains("\"status\"", response.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_Fixture_Maps_Application_Workflow_And_Persistence_Metadata()
    {
        var response = SymphonyResponseMapper.MapRuntime(ReadFixture("runtime-basic.json"));

        Assert.Equal("Symphony", response.ApplicationName);
        Assert.Equal("1.4.0+20260429", response.ApplicationVersion);
        Assert.Equal("conductor-api-main", response.InstanceId);
        Assert.Equal("poll-dispatch", response.LeaseName);
        Assert.Equal(120, response.LeaseTtlSeconds);
        Assert.Equal("sqlite", response.PersistenceProvider);
        Assert.True(response.PersistenceConfigured);
        Assert.Equal("/config/WORKFLOW.md", response.WorkflowSourcePath);
        Assert.Equal("ReleasedGroup", response.WorkflowOwner);
        Assert.Equal("TheConductor", response.WorkflowRepository);
        Assert.Equal("Sprint 3: Symphony API Client and Manual Registration", response.WorkflowMilestone);
        Assert.Equal(600000, response.PollingIntervalMs);
        Assert.Equal(5, response.MaxConcurrentAgents);
        Assert.Equal(20, response.MaxTurns);
        Assert.Equal("/var/lib/symphony/workspaces", response.WorkspaceRoot);
        Assert.Equal("main", response.WorkspaceBaseBranch);
        Assert.Null(response.WorkflowError);
    }

    [Theory]
    [InlineData("state-running.json", 1, 0, 3, 2900, true)]
    [InlineData("state-retrying.json", 0, 1, 2, 1020, false)]
    public void State_Fixture_Maps_Counts_Work_Items_And_Telemetry(
        string fixtureName,
        int runningCount,
        int retryingCount,
        int trackedCount,
        long totalTokens,
        bool hasRateLimits)
    {
        var response = SymphonyResponseMapper.MapState(ReadFixture(fixtureName));

        Assert.Equal(runningCount, response.RunningCount);
        Assert.Equal(retryingCount, response.RetryingCount);
        Assert.Equal(trackedCount, response.TrackedIssueCount);
        Assert.Equal(totalTokens, response.TokenTotals.TotalTokens);
        Assert.Equal(hasRateLimits, response.HasRateLimits);
        Assert.Contains(response.TrackedIssueDistribution, group => group.State == "Open");
        Assert.Single(response.Leases);

        if (runningCount > 0)
        {
            var running = Assert.Single(response.RunningSessions);
            Assert.Equal("#30", running.IssueIdentifier);
            Assert.Equal("thread-30-turn-3", running.SessionId);
            Assert.Equal(1550, running.Tokens.TotalTokens);
        }

        if (retryingCount > 0)
        {
            var retrying = Assert.Single(response.RetryQueue);
            Assert.Equal("#29", retrying.IssueIdentifier);
            Assert.Equal(2, retrying.Attempt);
            Assert.Equal("Previous run exited before producing a result.", retrying.Error);
        }
    }

    [Fact]
    public void Issue_Detail_Fixture_Maps_Runtime_Tracked_And_Link_Metadata()
    {
        var response = SymphonyResponseMapper.MapIssue(ReadFixture("issue-detail.json"));

        Assert.Equal("#30", response.IssueIdentifier);
        Assert.Equal("I_kwDOSPYGA85RUN", response.IssueId);
        Assert.Equal("running", response.Status);
        Assert.Equal(@"C:\workspaces\worktrees\30", response.WorkspacePath);
        Assert.Equal(1, response.RestartCount);
        Assert.Equal(2, response.CurrentRetryAttempt);
        Assert.True(response.Running is not null);
        var running = response.Running!;
        Assert.Equal("thread-30-turn-3", running.SessionId);
        Assert.Equal(1550, running.Tokens.TotalTokens);
        Assert.True(response.Retry is not null);
        var retry = response.Retry!;
        Assert.Equal("Previous attempt timed out.", retry.Error);
        Assert.Equal("Previous attempt timed out.", response.LastError);
        Assert.Equal("Add Symphony fixture mapping", response.Title);
        Assert.Equal("https://github.com/ReleasedGroup/TheConductor/issues/30", response.Url?.ToString());
        Assert.Equal(1, response.Priority);
        Assert.Contains("tests", response.Labels);
        Assert.Single(response.BlockedBy);
        Assert.Equal("#29", response.BlockedBy[0].Identifier);
        Assert.Single(response.PullRequests);
        Assert.Equal(12, response.PullRequests[0].Number);
        Assert.Equal("symphony/30", response.PullRequests[0].HeadRef);
        Assert.Equal(2, response.RecentEvents.Count);
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Symphony", fileName));
    }
}
