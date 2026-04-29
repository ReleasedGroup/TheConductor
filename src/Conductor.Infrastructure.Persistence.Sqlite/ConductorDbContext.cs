using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Events;
using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Operations;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Reports;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.SymphonyReleases;
using Conductor.Core.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContext(DbContextOptions<ConductorDbContext> options) : DbContext(options)
{
    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<BackgroundOperation> BackgroundOperations => Set<BackgroundOperation>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<TrackedIssue> TrackedIssues => Set<TrackedIssue>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Repository> Repositories => Set<Repository>();

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunAttempt> RunAttempts => Set<RunAttempt>();

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
