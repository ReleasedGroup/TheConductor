using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Infrastructure.Persistence.Sqlite.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class ConductorDatabaseBootstrapper
{
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    public static async Task BootstrapDevelopmentDatabaseAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await BootstrapLock.WaitAsync(cancellationToken);

        try
        {
            using IServiceScope scope = services.CreateScope();
            ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);
            await DevelopmentSeedData.SeedAsync(dbContext, cancellationToken);

            logger.LogInformation("Conductor development database is ready.");
        }
        finally
        {
            BootstrapLock.Release();
        }
    }
}

public static class DevelopmentSeedData
{
    public static readonly ProjectId PlatformProjectId = new(new Guid("7b6005b3-ef65-4ce0-b41b-6fa8e59527ed"));
    public static readonly ProjectId OperationsProjectId = new(new Guid("d92db4bb-bfa0-4fdf-8967-cf41bbbd0f6d"));
    public static readonly RepositoryId ConductorRepositoryId = new(new Guid("15357d28-1f50-4a93-8f1b-aa728cc9015d"));
    public static readonly RepositoryId SymphonyRepositoryId = new(new Guid("35592f7f-e79e-41c8-b468-e997ba3680ef"));
    public static readonly RepositoryId DeliveryRepositoryId = new(new Guid("de2bad81-5cf9-4f5e-af42-6f3a2cf256f0"));
    public static readonly SymphonyInstanceId ConductorInstanceId = new(new Guid("72111524-8fd3-4624-b54f-ee39039cbf33"));
    public static readonly SymphonyInstanceId SymphonyInstanceId = new(new Guid("ad43f9fe-ce9f-46ea-91d4-e2ce1a2ccded"));
    public static readonly SymphonyInstanceId DeliveryInstanceId = new(new Guid("aa30b68d-f7b9-46e9-8655-725b2f448eec"));
    public static readonly InstanceSnapshotId ConductorSnapshotId = new(new Guid("4964d22a-7692-4f7b-abd7-629b3567a2b1"));
    public static readonly InstanceSnapshotId SymphonySnapshotId = new(new Guid("051bbf4f-8132-4607-a54f-e021c9075ea0"));
    public static readonly InstanceSnapshotId DeliverySnapshotId = new(new Guid("8bc61322-a7fa-41d4-b15d-b5c229bdde99"));

    private static readonly DateTimeOffset SeededAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");

    public static async Task SeedAsync(ConductorDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        await SeedProjectsAsync(dbContext, cancellationToken);
        await SeedRepositoriesAsync(dbContext, cancellationToken);
        await SeedInstancesAsync(dbContext, cancellationToken);
        await SeedSnapshotsAsync(dbContext, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProjectsAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        DbSet<ProjectRecord> projects = dbContext.Set<ProjectRecord>();

        if (!await projects.AnyAsync(project => project.Id == Id(PlatformProjectId), cancellationToken))
        {
            projects.Add(new ProjectRecord
            {
                Id = Id(PlatformProjectId),
                Name = "Conductor Platform",
                OwnerName = "ReleasedGroup",
                Status = ProjectStatus.Active.ToString(),
                CreatedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc,
            });
        }

        if (!await projects.AnyAsync(project => project.Id == Id(OperationsProjectId), cancellationToken))
        {
            projects.Add(new ProjectRecord
            {
                Id = Id(OperationsProjectId),
                Name = "Operations Automation",
                OwnerName = "FactoryOps",
                Status = ProjectStatus.Active.ToString(),
                CreatedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc,
            });
        }
    }

    private static async Task SeedRepositoriesAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        DbSet<RepositoryRecord> repositories = dbContext.Set<RepositoryRecord>();

        if (!await repositories.AnyAsync(repository => repository.Id == Id(ConductorRepositoryId), cancellationToken))
        {
            repositories.Add(new RepositoryRecord
            {
                Id = Id(ConductorRepositoryId),
                ProjectId = Id(PlatformProjectId),
                Provider = RepositoryProvider.GitHub.ToString(),
                Owner = "ReleasedGroup",
                Name = "TheConductor",
                DefaultBranch = "main",
                CloneUrl = "https://github.com/ReleasedGroup/TheConductor.git",
                WebUrl = "https://github.com/ReleasedGroup/TheConductor",
                IsArchived = false,
                OpenIssueCount = 7,
                PullRequestCount = 2,
                ImportedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc.AddMinutes(18),
            });
        }

        if (!await repositories.AnyAsync(repository => repository.Id == Id(SymphonyRepositoryId), cancellationToken))
        {
            repositories.Add(new RepositoryRecord
            {
                Id = Id(SymphonyRepositoryId),
                ProjectId = Id(PlatformProjectId),
                Provider = RepositoryProvider.GitHub.ToString(),
                Owner = "ReleasedGroup",
                Name = "Symphony",
                DefaultBranch = "main",
                CloneUrl = "https://github.com/ReleasedGroup/Symphony.git",
                WebUrl = "https://github.com/ReleasedGroup/Symphony",
                IsArchived = false,
                OpenIssueCount = 12,
                PullRequestCount = 4,
                ImportedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc.AddMinutes(12),
            });
        }

        if (!await repositories.AnyAsync(repository => repository.Id == Id(DeliveryRepositoryId), cancellationToken))
        {
            repositories.Add(new RepositoryRecord
            {
                Id = Id(DeliveryRepositoryId),
                ProjectId = Id(OperationsProjectId),
                Provider = RepositoryProvider.GitHub.ToString(),
                Owner = "FactoryOps",
                Name = "delivery-reports",
                DefaultBranch = "main",
                CloneUrl = "https://github.com/FactoryOps/delivery-reports.git",
                WebUrl = "https://github.com/FactoryOps/delivery-reports",
                IsArchived = false,
                OpenIssueCount = 3,
                PullRequestCount = 1,
                ImportedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc.AddMinutes(4),
            });
        }
    }

    private static async Task SeedInstancesAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        DbSet<SymphonyInstanceRecord> instances = dbContext.Set<SymphonyInstanceRecord>();

        if (!await instances.AnyAsync(instance => instance.Id == Id(ConductorInstanceId), cancellationToken))
        {
            instances.Add(new SymphonyInstanceRecord
            {
                Id = Id(ConductorInstanceId),
                RepositoryId = Id(ConductorRepositoryId),
                DisplayName = "Conductor main",
                ExecutionMode = ExecutionMode.Docker.ToString(),
                BaseUrl = "http://localhost:8010",
                Status = InstanceLifecycleStatus.Running.ToString(),
                HealthStatus = InstanceHealthStatus.Healthy.ToString(),
                DeliveryStatus = "Healthy",
                ReleaseSelector = "latest",
                ResolvedReleaseTag = "v0.1.0-dev",
                CreatedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc.AddMinutes(18),
                LastHealthCheckAtUtc = SeededAtUtc.AddMinutes(18),
                LastSeenAtUtc = SeededAtUtc.AddMinutes(18),
            });
        }

        if (!await instances.AnyAsync(instance => instance.Id == Id(SymphonyInstanceId), cancellationToken))
        {
            instances.Add(new SymphonyInstanceRecord
            {
                Id = Id(SymphonyInstanceId),
                RepositoryId = Id(SymphonyRepositoryId),
                DisplayName = "Symphony release monitor",
                ExecutionMode = ExecutionMode.LocalProcess.ToString(),
                BaseUrl = "http://localhost:8020",
                Status = InstanceLifecycleStatus.Running.ToString(),
                HealthStatus = InstanceHealthStatus.Warning.ToString(),
                DeliveryStatus = "AttentionNeeded",
                ReleaseSelector = "v0.1.0-dev",
                ResolvedReleaseTag = "v0.1.0-dev",
                CreatedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc.AddMinutes(12),
                LastHealthCheckAtUtc = SeededAtUtc.AddMinutes(12),
                LastSeenAtUtc = SeededAtUtc.AddMinutes(12),
            });
        }

        if (!await instances.AnyAsync(instance => instance.Id == Id(DeliveryInstanceId), cancellationToken))
        {
            instances.Add(new SymphonyInstanceRecord
            {
                Id = Id(DeliveryInstanceId),
                RepositoryId = Id(DeliveryRepositoryId),
                DisplayName = "Delivery report worker",
                ExecutionMode = ExecutionMode.Docker.ToString(),
                BaseUrl = "http://localhost:8030",
                Status = InstanceLifecycleStatus.Stopped.ToString(),
                HealthStatus = InstanceHealthStatus.Offline.ToString(),
                DeliveryStatus = "Blocked",
                ReleaseSelector = "latest",
                CreatedAtUtc = SeededAtUtc,
                UpdatedAtUtc = SeededAtUtc.AddMinutes(4),
                LastHealthCheckAtUtc = SeededAtUtc.AddMinutes(4),
            });
        }
    }

    private static async Task SeedSnapshotsAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        DbSet<InstanceSnapshotRecord> snapshots = dbContext.Set<InstanceSnapshotRecord>();

        if (!await snapshots.AnyAsync(snapshot => snapshot.Id == Id(ConductorSnapshotId), cancellationToken))
        {
            snapshots.Add(new InstanceSnapshotRecord
            {
                Id = Id(ConductorSnapshotId),
                SymphonyInstanceId = Id(ConductorInstanceId),
                CapturedAtUtc = SeededAtUtc.AddMinutes(18),
                HealthStatus = InstanceHealthStatus.Healthy.ToString(),
                HttpStatusCode = 200,
                LatencyMilliseconds = 42,
                HealthJson = """{"status":"healthy","latencyMs":42}""",
                RuntimeJson = """{"version":"0.1.0-dev","mode":"docker"}""",
                StateJson = """{"running":3,"retrying":0,"tracked":8}""",
            });
        }

        if (!await snapshots.AnyAsync(snapshot => snapshot.Id == Id(SymphonySnapshotId), cancellationToken))
        {
            snapshots.Add(new InstanceSnapshotRecord
            {
                Id = Id(SymphonySnapshotId),
                SymphonyInstanceId = Id(SymphonyInstanceId),
                CapturedAtUtc = SeededAtUtc.AddMinutes(12),
                HealthStatus = InstanceHealthStatus.Warning.ToString(),
                HttpStatusCode = 200,
                LatencyMilliseconds = 118,
                HealthJson = """{"status":"warning","latencyMs":118}""",
                RuntimeJson = """{"version":"0.1.0-dev","mode":"local"}""",
                StateJson = """{"running":1,"retrying":2,"tracked":5}""",
            });
        }

        if (!await snapshots.AnyAsync(snapshot => snapshot.Id == Id(DeliverySnapshotId), cancellationToken))
        {
            snapshots.Add(new InstanceSnapshotRecord
            {
                Id = Id(DeliverySnapshotId),
                SymphonyInstanceId = Id(DeliveryInstanceId),
                CapturedAtUtc = SeededAtUtc.AddMinutes(4),
                HealthStatus = InstanceHealthStatus.Offline.ToString(),
                ErrorMessage = "Connection refused",
                HealthJson = """{"status":"offline","error":"connection refused"}""",
                StateJson = """{"running":0,"retrying":0,"tracked":3}""",
            });
        }
    }

    private static string Id(ProjectId id) => id.Value.ToString("D");

    private static string Id(RepositoryId id) => id.Value.ToString("D");

    private static string Id(SymphonyInstanceId id) => id.Value.ToString("D");

    private static string Id(InstanceSnapshotId id) => id.Value.ToString("D");
}
