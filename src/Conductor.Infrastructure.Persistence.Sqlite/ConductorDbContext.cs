using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContext(DbContextOptions<ConductorDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Repository> Repositories => Set<Repository>();

    public DbSet<SymphonyInstance> SymphonyInstances => Set<SymphonyInstance>();

    public DbSet<InstanceSnapshot> InstanceSnapshots => Set<InstanceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);

        ConfigureProjects(modelBuilder);
        ConfigureRepositories(modelBuilder);
        ConfigureSymphonyInstances(modelBuilder);
        ConfigureInstanceSnapshots(modelBuilder);
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<Project>();

        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id)
            .HasConversion(id => id.Value, value => new ProjectId(value))
            .ValueGeneratedNever();
        builder.Property(project => project.Name).HasMaxLength(160).IsRequired();
        builder.Property(project => project.OwnerName).HasMaxLength(160).IsRequired();
        builder.Property(project => project.Status).IsRequired();
        builder.Property(project => project.CreatedAtUtc)
            .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.Property(project => project.UpdatedAtUtc)
            .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
    }

    private static void ConfigureRepositories(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<Repository>();

        builder.ToTable("Repositories");
        builder.HasKey(repository => repository.Id);
        builder.Property(repository => repository.Id)
            .HasConversion(id => id.Value, value => new RepositoryId(value))
            .ValueGeneratedNever();
        builder.Property(repository => repository.ProjectId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new ProjectId(value.Value) : null);
        builder.Property(repository => repository.Provider).IsRequired();
        builder.Property(repository => repository.Owner).HasMaxLength(160).IsRequired();
        builder.Property(repository => repository.Name).HasMaxLength(160).IsRequired();
        builder.Property(repository => repository.DefaultBranch).HasMaxLength(160).IsRequired();
        builder.Property(repository => repository.CloneUrl)
            .HasConversion(uri => uri.ToString(), value => new Uri(value, UriKind.Absolute))
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(repository => repository.WebUrl)
            .HasConversion(uri => uri.ToString(), value => new Uri(value, UriKind.Absolute))
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(repository => repository.IsArchived).IsRequired();
        builder.Ignore(repository => repository.FullName);

        builder.HasIndex(repository => repository.ProjectId);
        builder.HasIndex(repository => new
        {
            repository.Provider,
            repository.Owner,
            repository.Name,
        })
            .IsUnique();
    }

    private static void ConfigureSymphonyInstances(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SymphonyInstance>();

        builder.ToTable("SymphonyInstances");
        builder.HasKey(instance => instance.Id);
        builder.Property(instance => instance.Id)
            .HasConversion(id => id.Value, value => new SymphonyInstanceId(value))
            .ValueGeneratedNever();
        builder.Property(instance => instance.RepositoryId)
            .HasConversion(id => id.Value, value => new RepositoryId(value))
            .IsRequired();
        builder.Property(instance => instance.DisplayName).HasMaxLength(240).IsRequired();
        builder.Property(instance => instance.ExecutionMode).IsRequired();
        builder.Property(instance => instance.BaseUrl)
            .HasConversion(uri => uri.ToString(), value => new Uri(value, UriKind.Absolute))
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(instance => instance.LifecycleStatus).IsRequired();
        builder.Property(instance => instance.HealthStatus).IsRequired();
        builder.Property(instance => instance.LastHealthCheckAtUtc)
            .HasConversion(
                value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
        builder.Property(instance => instance.LastSeenAtUtc)
            .HasConversion(
                value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

        builder.HasIndex(instance => instance.RepositoryId);
        builder.HasIndex(instance => new
        {
            instance.LifecycleStatus,
            instance.HealthStatus,
        });
        builder.HasIndex(instance => instance.ExecutionMode);
    }

    private static void ConfigureInstanceSnapshots(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<InstanceSnapshot>();

        builder.ToTable("InstanceSnapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id)
            .HasConversion(id => id.Value, value => new InstanceSnapshotId(value))
            .ValueGeneratedNever();
        builder.Property(snapshot => snapshot.SymphonyInstanceId)
            .HasConversion(id => id.Value, value => new SymphonyInstanceId(value));
        builder.Property(snapshot => snapshot.CapturedAtUtc)
            .HasConversion(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero))
            .IsRequired();
        builder.Property(snapshot => snapshot.HealthStatus).IsRequired();
        builder.Property(snapshot => snapshot.HealthJson);
        builder.Property(snapshot => snapshot.RuntimeJson);
        builder.Property(snapshot => snapshot.StateJson);
        builder.Property(snapshot => snapshot.ActiveIssueCount).IsRequired();
        builder.Property(snapshot => snapshot.RunningSessionCount).IsRequired();
        builder.Property(snapshot => snapshot.RetryQueueCount).IsRequired();
        builder.Property(snapshot => snapshot.FailedRunCount).IsRequired();
        builder.Property(snapshot => snapshot.TokenInputTotal).IsRequired();
        builder.Property(snapshot => snapshot.TokenOutputTotal).IsRequired();
        builder.Ignore(snapshot => snapshot.TokenTotal);

        builder.HasIndex(snapshot => new
        {
            snapshot.SymphonyInstanceId,
            snapshot.CapturedAtUtc,
        });
    }
}
