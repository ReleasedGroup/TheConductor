using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContext(DbContextOptions<ConductorDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<ProjectId, Guid> ProjectIdConverter =
        new(projectId => projectId.Value, value => new ProjectId(value));

    private static readonly ValueConverter<ProjectId?, Guid?> NullableProjectIdConverter =
        new(projectId => projectId.HasValue ? projectId.Value.Value : null, value => value.HasValue ? new ProjectId(value.Value) : null);

    private static readonly ValueConverter<RepositoryId, Guid> RepositoryIdConverter =
        new(repositoryId => repositoryId.Value, value => new RepositoryId(value));

    private static readonly ValueConverter<SymphonyInstanceId, Guid> SymphonyInstanceIdConverter =
        new(instanceId => instanceId.Value, value => new SymphonyInstanceId(value));

    private static readonly ValueConverter<Uri, string> UriConverter =
        new(uri => uri.ToString(), value => new Uri(value));

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
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(project => project.Id);

            entity.Property(project => project.Id)
                .HasConversion(ProjectIdConverter)
                .ValueGeneratedNever();

            entity.Property(project => project.Name)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(project => project.OwnerName)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(project => project.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(project => project.CreatedAtUtc)
                .IsRequired();

            entity.Property(project => project.UpdatedAtUtc)
                .IsRequired();
        });
    }

    private static void ConfigureRepositories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.ToTable("Repositories");
            entity.HasKey(repository => repository.Id);

            entity.Property(repository => repository.Id)
                .HasConversion(RepositoryIdConverter)
                .ValueGeneratedNever();

            entity.Property(repository => repository.Provider)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(repository => repository.Owner)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(repository => repository.Name)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(repository => repository.DefaultBranch)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(repository => repository.CloneUrl)
                .HasConversion(UriConverter)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(repository => repository.WebUrl)
                .HasConversion(UriConverter)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(repository => repository.IsArchived)
                .IsRequired();

            entity.Property(repository => repository.ProjectId)
                .HasConversion(NullableProjectIdConverter);

            entity.Ignore(repository => repository.FullName);

            entity.HasIndex(repository => repository.ProjectId);
            entity.HasIndex(repository => new { repository.Provider, repository.Owner, repository.Name })
                .IsUnique();

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(repository => repository.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureSymphonyInstances(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SymphonyInstance>(entity =>
        {
            entity.ToTable("SymphonyInstances");
            entity.HasKey(instance => instance.Id);

            entity.Property(instance => instance.Id)
                .HasConversion(SymphonyInstanceIdConverter)
                .ValueGeneratedNever();

            entity.Property(instance => instance.RepositoryId)
                .HasConversion(RepositoryIdConverter)
                .IsRequired();

            entity.Property(instance => instance.DisplayName)
                .HasMaxLength(240)
                .IsRequired();

            entity.Property(instance => instance.ExecutionMode)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(instance => instance.BaseUrl)
                .HasConversion(UriConverter)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(instance => instance.LifecycleStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(instance => instance.HealthStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.HasIndex(instance => instance.RepositoryId);
            entity.HasIndex(instance => new { instance.LifecycleStatus, instance.HealthStatus });
            entity.HasIndex(instance => instance.ExecutionMode);

            entity.HasOne<Repository>()
                .WithMany()
                .HasForeignKey(instance => instance.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureInstanceSnapshots(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstanceSnapshot>(entity =>
        {
            entity.ToTable("InstanceSnapshots");
            entity.HasKey(snapshot => new { snapshot.SymphonyInstanceId, snapshot.CapturedAtUtc });

            entity.Property(snapshot => snapshot.SymphonyInstanceId)
                .HasConversion(SymphonyInstanceIdConverter)
                .IsRequired();

            entity.Property(snapshot => snapshot.HealthStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(snapshot => snapshot.HealthJson);
            entity.Property(snapshot => snapshot.RuntimeJson);
            entity.Property(snapshot => snapshot.StateJson);

            entity.HasIndex(snapshot => new { snapshot.SymphonyInstanceId, snapshot.CapturedAtUtc });

            entity.HasOne<SymphonyInstance>()
                .WithMany()
                .HasForeignKey(snapshot => snapshot.SymphonyInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
