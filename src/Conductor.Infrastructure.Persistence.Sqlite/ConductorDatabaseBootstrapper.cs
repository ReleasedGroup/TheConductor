using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
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
        await SeedProjectAsync(
            dbContext,
            PlatformProjectId,
            "Conductor Platform",
            "ReleasedGroup",
            "Local Conductor development fleet.",
            cancellationToken);

        await SeedProjectAsync(
            dbContext,
            OperationsProjectId,
            "Operations Automation",
            "FactoryOps",
            "Automation repositories used for development smoke tests.",
            cancellationToken);
    }

    private static async Task SeedRepositoriesAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedRepositoryAsync(
            dbContext,
            ConductorRepositoryId,
            PlatformProjectId,
            "ReleasedGroup",
            "TheConductor",
            SeededAtUtc.AddMinutes(18),
            cancellationToken);

        await SeedRepositoryAsync(
            dbContext,
            SymphonyRepositoryId,
            PlatformProjectId,
            "ReleasedGroup",
            "Symphony",
            SeededAtUtc.AddMinutes(12),
            cancellationToken);

        await SeedRepositoryAsync(
            dbContext,
            DeliveryRepositoryId,
            OperationsProjectId,
            "FactoryOps",
            "delivery-reports",
            SeededAtUtc.AddMinutes(4),
            cancellationToken);
    }

    private static async Task SeedInstancesAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedInstanceAsync(
            dbContext,
            ConductorInstanceId,
            ConductorRepositoryId,
            "Conductor main",
            ExecutionMode.Docker,
            "http://localhost:8010",
            port: 8010,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy,
            SeededAtUtc.AddMinutes(18),
            cancellationToken);

        await SeedInstanceAsync(
            dbContext,
            SymphonyInstanceId,
            SymphonyRepositoryId,
            "Symphony release monitor",
            ExecutionMode.LocalProcess,
            "http://localhost:8020",
            port: 8020,
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Warning,
            SeededAtUtc.AddMinutes(12),
            cancellationToken);

        await SeedInstanceAsync(
            dbContext,
            DeliveryInstanceId,
            DeliveryRepositoryId,
            "Delivery report worker",
            ExecutionMode.Docker,
            "http://localhost:8030",
            port: 8030,
            InstanceLifecycleStatus.Stopped,
            InstanceHealthStatus.Offline,
            SeededAtUtc.AddMinutes(4),
            cancellationToken);
    }

    private static async Task SeedSnapshotsAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedSnapshotAsync(
            dbContext,
            ConductorSnapshotId,
            ConductorInstanceId,
            SeededAtUtc.AddMinutes(18),
            InstanceHealthStatus.Healthy,
            """{"status":"healthy","latencyMs":42}""",
            """{"version":"0.1.0-dev","mode":"docker"}""",
            """{"running":3,"retrying":0,"tracked":8}""",
            activeIssueCount: 8,
            runningSessionCount: 3,
            retryQueueCount: 0,
            failedRunCount: 0,
            cancellationToken);

        await SeedSnapshotAsync(
            dbContext,
            SymphonySnapshotId,
            SymphonyInstanceId,
            SeededAtUtc.AddMinutes(12),
            InstanceHealthStatus.Warning,
            """{"status":"warning","latencyMs":118}""",
            """{"version":"0.1.0-dev","mode":"local"}""",
            """{"running":1,"retrying":2,"tracked":5}""",
            activeIssueCount: 5,
            runningSessionCount: 1,
            retryQueueCount: 2,
            failedRunCount: 1,
            cancellationToken);

        await SeedSnapshotAsync(
            dbContext,
            DeliverySnapshotId,
            DeliveryInstanceId,
            SeededAtUtc.AddMinutes(4),
            InstanceHealthStatus.Offline,
            """{"status":"offline","error":"connection refused"}""",
            null,
            """{"running":0,"retrying":0,"tracked":3}""",
            activeIssueCount: 3,
            runningSessionCount: 0,
            retryQueueCount: 0,
            failedRunCount: 2,
            cancellationToken);
    }

    private static async Task SeedProjectAsync(
        ConductorDbContext dbContext,
        ProjectId projectId,
        string name,
        string ownerName,
        string description,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Projects.AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return;
        }

        dbContext.Projects.Add(new Project(
            projectId,
            name,
            ownerName,
            description,
            "main",
            ProjectStatus.Active,
            SeededAtUtc,
            SeededAtUtc));
    }

    private static async Task SeedRepositoryAsync(
        ConductorDbContext dbContext,
        RepositoryId repositoryId,
        ProjectId projectId,
        string owner,
        string name,
        DateTimeOffset lastSyncedAtUtc,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Repositories.AnyAsync(repository => repository.Id == repositoryId, cancellationToken))
        {
            return;
        }

        dbContext.Repositories.Add(new Repository(
            repositoryId,
            RepositoryProvider.GitHub,
            owner,
            name,
            "main",
            new Uri($"https://github.com/{owner}/{name}.git"),
            new Uri($"https://github.com/{owner}/{name}"),
            RepositoryVisibility.Public,
            isArchived: false,
            projectId,
            lastSyncedAtUtc,
            RepositoryOrchestrationStatus.Eligible,
            orchestrationStatusReason: null));
    }

    private static async Task SeedInstanceAsync(
        ConductorDbContext dbContext,
        SymphonyInstanceId instanceId,
        RepositoryId repositoryId,
        string displayName,
        ExecutionMode executionMode,
        string baseUrl,
        int port,
        InstanceLifecycleStatus lifecycleStatus,
        InstanceHealthStatus healthStatus,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (await dbContext.SymphonyInstances.AnyAsync(instance => instance.Id == instanceId, cancellationToken))
        {
            return;
        }

        var instance = new SymphonyInstance(
            instanceId,
            repositoryId,
            displayName,
            executionMode,
            new Uri(baseUrl),
            SeededAtUtc,
            lifecycleStatus,
            healthStatus,
            port,
            symphonyVersion: "0.1.0-dev",
            symphonyReleaseTag: "v0.1.0-dev",
            lastStartedAtUtc: lifecycleStatus == InstanceLifecycleStatus.Running ? observedAtUtc : null);

        instance.RecordHealth(healthStatus, observedAtUtc);
        dbContext.SymphonyInstances.Add(instance);
    }

    private static async Task SeedSnapshotAsync(
        ConductorDbContext dbContext,
        InstanceSnapshotId snapshotId,
        SymphonyInstanceId instanceId,
        DateTimeOffset capturedAtUtc,
        InstanceHealthStatus healthStatus,
        string? healthJson,
        string? runtimeJson,
        string? stateJson,
        int activeIssueCount,
        int runningSessionCount,
        int retryQueueCount,
        int failedRunCount,
        CancellationToken cancellationToken)
    {
        if (await dbContext.InstanceSnapshots.AnyAsync(snapshot => snapshot.Id == snapshotId, cancellationToken))
        {
            return;
        }

        dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
            snapshotId,
            instanceId,
            capturedAtUtc,
            healthStatus,
            healthJson,
            runtimeJson,
            stateJson,
            activeIssueCount,
            runningSessionCount,
            retryQueueCount,
            failedRunCount,
            tokenInputTotal: 100,
            tokenOutputTotal: 40));
    }
}
