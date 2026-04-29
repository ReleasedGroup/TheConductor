using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.SymphonyReleases;
using Conductor.Core.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContext(DbContextOptions<ConductorDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Repository> Repositories => Set<Repository>();

    public DbSet<SymphonyInstance> SymphonyInstances => Set<SymphonyInstance>();

    public DbSet<WorkflowProfile> WorkflowProfiles => Set<WorkflowProfile>();

    public DbSet<SymphonyReleaseArtifact> SymphonyReleaseArtifacts => Set<SymphonyReleaseArtifact>();

    public DbSet<InstanceSnapshot> InstanceSnapshots => Set<InstanceSnapshot>();

    public DbSet<SecretDescriptor> SecretDescriptors => Set<SecretDescriptor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConductorDbContext).Assembly);
    }
}
