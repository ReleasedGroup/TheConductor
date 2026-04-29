using Conductor.Core.Domain;
using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Events;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Operations;
using Conductor.Core.Domain.Reports;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Snapshots;

namespace Conductor.Core.Tests;

public sealed class OperationalDomainEntityTests
{
    [Fact]
    public void InstanceSnapshot_Trims_Raw_Json_And_Tracks_Aggregates()
    {
        var snapshot = new InstanceSnapshot(
            InstanceSnapshotId.New(),
            SymphonyInstanceId.New(),
            DateTimeOffset.Parse("2026-04-29T01:00:00Z"),
            InstanceHealthStatus.Warning,
            "  {\"status\":\"warning\"}  ",
            "   ",
            null,
            activeIssueCount: 8,
            runningSessionCount: 2,
            retryQueueCount: 1,
            failedRunCount: 3,
            tokenInputTotal: 120,
            tokenOutputTotal: 30);

        Assert.Equal("{\"status\":\"warning\"}", snapshot.HealthJson);
        Assert.Null(snapshot.RuntimeJson);
        Assert.Null(snapshot.StateJson);
        Assert.Equal(150, snapshot.TokenTotal);
    }

    [Fact]
    public void TrackedIssue_Normalizes_Metadata_And_Blocker_State()
    {
        var lastActivityAt = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        var blockedAt = lastActivityAt.AddMinutes(10);
        var clearedAt = blockedAt.AddMinutes(5);
        var issue = new TrackedIssue(
            TrackedIssueId.New(),
            RepositoryId.New(),
            gitHubIssueNumber: 14,
            "  Implement entities  ",
            TrackedIssueState.Open,
            " [\"domain\"] ",
            " Sprint 1 ",
            " [\"nickbeau\"] ",
            new Uri("https://github.com/ReleasedGroup/TheConductor/issues/14"),
            SymphonyIssueStatus.Queued,
            RunStatus.Running,
            lastActivityAt,
            isBlocked: false,
            blockerReason: " ignored ");

        issue.MarkBlocked(" Waiting on dependency ", blockedAt);
        issue.ClearBlocker(clearedAt);
        issue.UpdateSymphonyStatus(SymphonyIssueStatus.Succeeded, RunStatus.Succeeded, clearedAt.AddMinutes(1));

        Assert.Equal("Implement entities", issue.Title);
        Assert.Equal("[\"domain\"]", issue.LabelsJson);
        Assert.Equal("Sprint 1", issue.Milestone);
        Assert.Equal("[\"nickbeau\"]", issue.AssigneeLoginsJson);
        Assert.False(issue.IsBlocked);
        Assert.Null(issue.BlockerReason);
        Assert.Equal(SymphonyIssueStatus.Succeeded, issue.SymphonyStatus);
        Assert.Equal(RunStatus.Succeeded, issue.LastRunStatus);
    }

    [Fact]
    public void Run_And_Attempt_Track_Terminal_State_And_Tokens()
    {
        var startedAt = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        var finishedAt = startedAt.AddMinutes(8);
        var run = new Run(
            RunId.New(),
            SymphonyInstanceId.New(),
            RepositoryId.New(),
            gitHubIssueNumber: 14,
            symphonyRunId: "  symphony-run-1  ",
            RunStatus.Running,
            startedAt,
            finishedAtUtc: null,
            attemptCount: 0,
            tokenInput: 0,
            tokenOutput: 0,
            errorSummary: null,
            branchName: " symphony/14 ",
            pullRequestUrl: new Uri("https://github.com/ReleasedGroup/TheConductor/pull/116"));
        var attempt = new RunAttempt(
            RunAttemptId.New(),
            run.Id,
            attemptNumber: 1,
            RunStatus.Running,
            startedAt,
            finishedAtUtc: null,
            exitReason: null,
            errorDetail: null);

        run.RecordAttempt();
        run.RecordTokenUsage(400, 125);
        attempt.Complete(RunStatus.Succeeded, finishedAt, " completed ", "  ");
        run.Complete(RunStatus.Succeeded, finishedAt, errorSummary: null);

        Assert.Equal("symphony-run-1", run.SymphonyRunId);
        Assert.Equal("symphony/14", run.BranchName);
        Assert.Equal(1, run.AttemptCount);
        Assert.Equal(525, run.TokenTotal);
        Assert.Equal(finishedAt, run.FinishedAtUtc);
        Assert.Equal(RunStatus.Succeeded, attempt.Status);
        Assert.Equal("completed", attempt.ExitReason);
        Assert.Null(attempt.ErrorDetail);
    }

    [Fact]
    public void Run_Rejects_Non_Terminal_Completion()
    {
        var startedAt = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        var run = new Run(
            RunId.New(),
            SymphonyInstanceId.New(),
            RepositoryId.New(),
            gitHubIssueNumber: 14,
            symphonyRunId: null,
            RunStatus.Running,
            startedAt,
            finishedAtUtc: null,
            attemptCount: 0,
            tokenInput: 0,
            tokenOutput: 0,
            errorSummary: null,
            branchName: null,
            pullRequestUrl: null);

        Assert.Throws<ArgumentException>(() =>
            run.Complete(RunStatus.Running, startedAt.AddMinutes(1), errorSummary: null));
    }

    [Fact]
    public void Event_Alert_Report_Audit_And_BackgroundOperation_Capture_Operational_Metadata()
    {
        var occurredAt = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        var repositoryId = RepositoryId.New();
        var instanceId = SymphonyInstanceId.New();
        var runId = RunId.New();
        var recordedEvent = new Event(
            EventId.New(),
            instanceId,
            repositoryId,
            issueNumber: 14,
            EventSeverity.Warning,
            "  RunFailed  ",
            "  Attempt failed  ",
            " {\"attempt\":1} ",
            occurredAt);
        var alert = new Alert(
            AlertId.New(),
            AlertSeverity.Critical,
            "  run-monitor  ",
            "  Repeated run failures  ",
            "  Inspect attempt logs  ",
            occurredAt,
            instanceId,
            repositoryId,
            runId,
            gitHubIssueNumber: 14);
        var report = new Report(
            ReportId.New(),
            ReportType.WeeklySoftwareFactory,
            " all-projects ",
            occurredAt.AddDays(-7),
            occurredAt,
            occurredAt,
            " # Weekly ",
            " <h1>Weekly</h1> ",
            " reports/weekly.html ",
            " {\"source\":\"test\"} ");
        var auditEvent = new AuditEvent(
            AuditEventId.New(),
            "  nickbeau  ",
            "  ResolveAlert  ",
            "  Alert  ",
            alert.Id.ToString(),
            occurredAt.AddMinutes(1),
            AuditEventOutcome.Succeeded,
            " correlation-1 ",
            "  Resolved from alert center  ",
            null);
        var operation = new BackgroundOperation(
            BackgroundOperationId.New(),
            "  GenerateReport  ",
            occurredAt,
            "Report",
            report.Id.ToString(),
            " correlation-1 ");

        alert.Acknowledge();
        alert.Resolve(occurredAt.AddMinutes(2), " recovered ");
        operation.MarkRunning(occurredAt.AddMinutes(1));
        operation.Succeed(occurredAt.AddMinutes(3), " completed ");

        Assert.Equal("RunFailed", recordedEvent.EventType);
        Assert.Equal("Attempt failed", recordedEvent.Message);
        Assert.Equal("{\"attempt\":1}", recordedEvent.PayloadJson);
        Assert.Equal(AlertStatus.Resolved, alert.Status);
        Assert.Equal("recovered", alert.ResolutionNote);
        Assert.Equal("all-projects", report.Scope);
        Assert.Equal("# Weekly", report.Markdown);
        Assert.Equal("<h1>Weekly</h1>", report.Html);
        Assert.Equal("{\"source\":\"test\"}", report.MetadataJson);
        Assert.Equal("nickbeau", auditEvent.ActorUserId);
        Assert.Equal("ResolveAlert", auditEvent.Action);
        Assert.Equal(BackgroundOperationStatus.Succeeded, operation.Status);
        Assert.Equal("completed", operation.Summary);
    }

    [Fact]
    public void BackgroundOperation_Rejects_Out_Of_Order_Timestamps()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        var operation = new BackgroundOperation(
            BackgroundOperationId.New(),
            "ProvisionInstance",
            createdAt,
            targetResourceType: null,
            targetResourceId: null,
            correlationId: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => operation.MarkRunning(createdAt.AddSeconds(-1)));
    }
}
