using Conductor.Core.Application.Dashboard;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Dashboard;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        string projectId = Guid.NewGuid().ToString();
        string repositoryId = Guid.NewGuid().ToString();
        string instanceId = Guid.NewGuid().ToString();
        string trackedIssueId = Guid.NewGuid().ToString();
        string failedRunId = Guid.NewGuid().ToString();
        string completedRunId = Guid.NewGuid().ToString();

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Projects (Id, Name, OwnerName, Status, CreatedAtUtc, UpdatedAtUtc)
            VALUES ({projectId}, {"ACME Portal"}, {"Platform"}, {"Active"}, {now.AddDays(-4)}, {now.AddHours(-2)});
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Repositories (
                Id,
                ProjectId,
                Provider,
                Owner,
                Name,
                DefaultBranch,
                CloneUrl,
                WebUrl,
                IsArchived,
                OpenIssueCount,
                PullRequestCount,
                ImportedAtUtc,
                UpdatedAtUtc)
            VALUES (
                {repositoryId},
                {projectId},
                {"GitHub"},
                {"releasedgroup"},
                {"acme-portal"},
                {"main"},
                {"https://github.com/releasedgroup/acme-portal.git"},
                {"https://github.com/releasedgroup/acme-portal"},
                {false},
                {9},
                {3},
                {now.AddDays(-3)},
                {now.AddHours(-1)});
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SymphonyInstances (
                Id,
                RepositoryId,
                DisplayName,
                ExecutionMode,
                BaseUrl,
                Status,
                HealthStatus,
                DeliveryStatus,
                CreatedAtUtc,
                UpdatedAtUtc,
                LastHealthCheckAtUtc,
                LastSeenAtUtc)
            VALUES (
                {instanceId},
                {repositoryId},
                {"acme-portal agent"},
                {"Docker"},
                {"http://localhost:5101"},
                {"Running"},
                {"Warning"},
                {"AttentionNeeded"},
                {now.AddDays(-2)},
                {now.AddMinutes(-20)},
                {now.AddMinutes(-20)},
                {now.AddMinutes(-20)});
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TrackedIssues (
                Id,
                RepositoryId,
                GitHubIssueNumber,
                Title,
                SymphonyStatus,
                IsBlocked,
                UpdatedAtUtc)
            VALUES (
                {trackedIssueId},
                {repositoryId},
                {25},
                {"Build active repositories table"},
                {"Running"},
                {false},
                {now.AddMinutes(-10)});
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Runs (
                Id,
                SymphonyInstanceId,
                RepositoryId,
                GitHubIssueNumber,
                Status,
                StartedAtUtc,
                CompletedAtUtc)
            VALUES (
                {failedRunId},
                {instanceId},
                {repositoryId},
                {25},
                {"Failed"},
                {now.AddMinutes(-30)},
                {now.AddMinutes(-25)});
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Runs (
                Id,
                SymphonyInstanceId,
                RepositoryId,
                GitHubIssueNumber,
                Status,
                StartedAtUtc,
                CompletedAtUtc)
            VALUES (
                {completedRunId},
                {instanceId},
                {repositoryId},
                {24},
                {"Succeeded"},
                {now.AddMinutes(-8)},
                {now.AddMinutes(-6)});
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Events (
                Id,
                SymphonyInstanceId,
                RepositoryId,
                EventType,
                Severity,
                Message,
                OccurredAtUtc)
            VALUES (
                {Guid.NewGuid().ToString()},
                {instanceId},
                {repositoryId},
                {"RunQueued"},
                {"Information"},
                {"Run queued for dashboard issue."},
                {now.AddMinutes(-2)});
            """);

        IActiveRepositoryDashboardQuery query = new SqliteActiveRepositoryDashboardQuery(dbContext);

        ActiveRepositoryDashboard dashboard = await query.LoadAsync();

        ActiveRepositoryDashboardRow row = Assert.Single(dashboard.Rows);
        Assert.Equal("ACME Portal", row.ProjectName);
        Assert.Equal("releasedgroup/acme-portal", row.RepositoryFullName);
        Assert.Equal(DashboardRepositoryHealth.Warning, row.Health);
        Assert.Equal(1, row.ActiveIssueCount);
        Assert.Equal(1, row.RunningAgentCount);
        Assert.Equal(1, row.FailedRunCount);
        Assert.Equal(3, row.OpenPullRequestCount);
        Assert.Equal(now.AddMinutes(-2), row.LastActivityAtUtc);
    }
}
