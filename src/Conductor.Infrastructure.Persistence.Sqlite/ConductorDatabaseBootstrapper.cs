using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public static class ConductorDatabaseBootstrapper
{
    public static async Task BootstrapDevelopmentDatabaseAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        EnsureDatabaseDirectory(dbContext);

        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

            string[] migrations = [.. dbContext.Database.GetMigrations()];
            if (migrations.Length > 0)
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            await DevelopmentSeedData.SeedAsync(dbContext, cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        logger.LogInformation("Conductor development database is ready.");
    }

    private static void EnsureDatabaseDirectory(ConductorDbContext dbContext)
    {
        string? connectionString = dbContext.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string fullPath = Path.GetFullPath(builder.DataSource);
        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
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
        if (!await dbContext.Projects.AnyAsync(project => project.Id == PlatformProjectId, cancellationToken))
        {
            dbContext.Projects.Add(new Project(
                PlatformProjectId,
                "Conductor Platform",
                "ReleasedGroup",
                ProjectStatus.Active,
                SeededAtUtc,
                SeededAtUtc));
        }

        if (!await dbContext.Projects.AnyAsync(project => project.Id == OperationsProjectId, cancellationToken))
        {
            dbContext.Projects.Add(new Project(
                OperationsProjectId,
                "Operations Automation",
                "FactoryOps",
                ProjectStatus.Active,
                SeededAtUtc,
                SeededAtUtc));
        }
    }

    private static async Task SeedRepositoriesAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Repositories.AnyAsync(repository => repository.Id == ConductorRepositoryId, cancellationToken))
        {
            dbContext.Repositories.Add(new Repository(
                ConductorRepositoryId,
                RepositoryProvider.GitHub,
                "ReleasedGroup",
                "TheConductor",
                "main",
                new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
                new Uri("https://github.com/ReleasedGroup/TheConductor"),
                isArchived: false,
                PlatformProjectId));
        }

        if (!await dbContext.Repositories.AnyAsync(repository => repository.Id == SymphonyRepositoryId, cancellationToken))
        {
            dbContext.Repositories.Add(new Repository(
                SymphonyRepositoryId,
                RepositoryProvider.GitHub,
                "ReleasedGroup",
                "Symphony",
                "main",
                new Uri("https://github.com/ReleasedGroup/Symphony.git"),
                new Uri("https://github.com/ReleasedGroup/Symphony"),
                isArchived: false,
                PlatformProjectId));
        }

        if (!await dbContext.Repositories.AnyAsync(repository => repository.Id == DeliveryRepositoryId, cancellationToken))
        {
            dbContext.Repositories.Add(new Repository(
                DeliveryRepositoryId,
                RepositoryProvider.GitHub,
                "FactoryOps",
                "delivery-reports",
                "main",
                new Uri("https://github.com/FactoryOps/delivery-reports.git"),
                new Uri("https://github.com/FactoryOps/delivery-reports"),
                isArchived: false,
                OperationsProjectId));
        }
    }

    private static async Task SeedInstancesAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.SymphonyInstances.AnyAsync(instance => instance.Id == ConductorInstanceId, cancellationToken))
        {
            var instance = new SymphonyInstance(
                ConductorInstanceId,
                ConductorRepositoryId,
                "Conductor main",
                ExecutionMode.Docker,
                new Uri("http://localhost:8010"),
                InstanceLifecycleStatus.Running,
                InstanceHealthStatus.Healthy);

            instance.RecordHealth(InstanceHealthStatus.Healthy, SeededAtUtc.AddMinutes(18));
            dbContext.SymphonyInstances.Add(instance);
        }

        if (!await dbContext.SymphonyInstances.AnyAsync(instance => instance.Id == SymphonyInstanceId, cancellationToken))
        {
            var instance = new SymphonyInstance(
                SymphonyInstanceId,
                SymphonyRepositoryId,
                "Symphony release monitor",
                ExecutionMode.LocalProcess,
                new Uri("http://localhost:8020"),
                InstanceLifecycleStatus.Running,
                InstanceHealthStatus.Warning);

            instance.RecordHealth(InstanceHealthStatus.Warning, SeededAtUtc.AddMinutes(12));
            dbContext.SymphonyInstances.Add(instance);
        }

        if (!await dbContext.SymphonyInstances.AnyAsync(instance => instance.Id == DeliveryInstanceId, cancellationToken))
        {
            var instance = new SymphonyInstance(
                DeliveryInstanceId,
                DeliveryRepositoryId,
                "Delivery report worker",
                ExecutionMode.Docker,
                new Uri("http://localhost:8030"),
                InstanceLifecycleStatus.Stopped,
                InstanceHealthStatus.Offline);

            instance.RecordHealth(InstanceHealthStatus.Offline, SeededAtUtc.AddMinutes(4));
            dbContext.SymphonyInstances.Add(instance);
        }
    }

    private static async Task SeedSnapshotsAsync(ConductorDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.InstanceSnapshots.AnyAsync(snapshot => snapshot.SymphonyInstanceId == ConductorInstanceId, cancellationToken))
        {
            dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
                ConductorInstanceId,
                SeededAtUtc.AddMinutes(18),
                InstanceHealthStatus.Healthy,
                """{"status":"healthy","latencyMs":42}""",
                """{"version":"0.1.0-dev","mode":"docker"}""",
                """{"running":3,"retrying":0,"tracked":8}"""));
        }

        if (!await dbContext.InstanceSnapshots.AnyAsync(snapshot => snapshot.SymphonyInstanceId == SymphonyInstanceId, cancellationToken))
        {
            dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
                SymphonyInstanceId,
                SeededAtUtc.AddMinutes(12),
                InstanceHealthStatus.Warning,
                """{"status":"warning","latencyMs":118}""",
                """{"version":"0.1.0-dev","mode":"local"}""",
                """{"running":1,"retrying":2,"tracked":5}"""));
        }

        if (!await dbContext.InstanceSnapshots.AnyAsync(snapshot => snapshot.SymphonyInstanceId == DeliveryInstanceId, cancellationToken))
        {
            dbContext.InstanceSnapshots.Add(new InstanceSnapshot(
                DeliveryInstanceId,
                SeededAtUtc.AddMinutes(4),
                InstanceHealthStatus.Offline,
                """{"status":"offline","error":"connection refused"}""",
                null,
                """{"running":0,"retrying":0,"tracked":3}"""));
        }
    }
}
