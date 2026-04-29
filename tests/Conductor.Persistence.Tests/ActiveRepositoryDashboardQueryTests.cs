using Conductor.Core.Application.Dashboard;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Dashboard;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainEvent = Conductor.Core.Domain.Events.Event;

namespace Conductor.Persistence.Tests;

public sealed class ActiveRepositoryDashboardQueryTests
{
    [Fact]
    public async Task LoadAsync_Projects_Active_Repository_Workload_From_Persistence()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ConductorDbContext dbContext = new(options);
        await dbContext.Database.MigrateAsync();

        DateTimeOffset now = new(2026, 4, 29, 1, 0, 0, TimeSpan.Zero);
        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();

        dbContext.Projects.Add(new Project(
            projectId,
            "ACME Portal",
            "Platform",
            "Dashboard seed",
            "main",
            ProjectStatus.Active,
            now.AddDays(-4),
            now.AddHours(-2)));

        dbContext.Repositories.Add(new Repository(
            repositoryId,
            RepositoryProvider.GitHub,
            "releasedgroup",
            "acme-portal",
            "main",
            new Uri("https://github.com/releasedgroup/acme-portal.git"),
            new Uri("https://github.com/releasedgroup/acme-portal"),
            RepositoryVisibility.Public,
            false,
            projectId,
            now.AddHours(-1),
            RepositoryOrchestrationStatus.Eligible,
            null));

        var instance = new SymphonyInstance(
            instanceId,
            repositoryId,
            "acme-portal agent",
            ExecutionMode.Docker,
            new Uri("http://localhost:5101"),
            now.AddDays(-2),
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Unknown,
            lastSeenAtUtc: now.AddMinutes(-20));
        instance.RecordHealth(InstanceHealthStatus.Warning, now.AddMinutes(-20));
        dbContext.SymphonyInstances.Add(instance);

        dbContext.TrackedIssues.Add(new TrackedIssue(
            TrackedIssueId.New(),
            repositoryId,
            25,
            "Build active repositories table",
            TrackedIssueState.Open,
            null,
            null,
            null,
            new Uri("https://github.com/ReleasedGroup/TheConductor/issues/25"),
            SymphonyIssueStatus.Running,
            RunStatus.Running,
            now.AddMinutes(-10),
            false,
            null));

        dbContext.Runs.Add(new Run(
            RunId.New(),
            instanceId,
            repositoryId,
            25,
            "symphony-run-25",
            RunStatus.Failed,
            now.AddMinutes(-30),
            now.AddMinutes(-25),
            1,
            100,
            40,
            "Tests failed.",
            "symphony/25",
            null));

        dbContext.Runs.Add(new Run(
            RunId.New(),
            instanceId,
            repositoryId,
            24,
            "symphony-run-24",
            RunStatus.Running,
            now.AddMinutes(-8),
            null,
            1,
            75,
            20,
            null,
            "symphony/24",
            new Uri("https://github.com/releasedgroup/acme-portal/pull/24")));

        dbContext.Events.Add(new DomainEvent(
            EventId.New(),
            instanceId,
            repositoryId,
            25,
            EventSeverity.Information,
            "RunQueued",
            "Run queued for dashboard issue.",
            null,
            now.AddMinutes(-2)));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        IActiveRepositoryDashboardQuery query = new SqliteActiveRepositoryDashboardQuery(dbContext);

        ActiveRepositoryDashboard dashboard = await query.LoadAsync();

        ActiveRepositoryDashboardRow row = Assert.Single(dashboard.Rows);
        Assert.Equal("ACME Portal", row.ProjectName);
        Assert.Equal("releasedgroup/acme-portal", row.RepositoryFullName);
        Assert.Equal(DashboardRepositoryHealth.Warning, row.Health);
        Assert.Equal(1, row.ActiveIssueCount);
        Assert.Equal(1, row.RunningAgentCount);
        Assert.Equal(1, row.FailedRunCount);
        Assert.Equal(1, row.OpenPullRequestCount);
        Assert.Equal(now.AddMinutes(-2), row.LastActivityAtUtc);
    }
}
