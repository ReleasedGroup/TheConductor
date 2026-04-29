using Conductor.Infrastructure.Persistence.Sqlite.Schema;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite;

internal static class InitialSchemaModelBuilderExtensions
{
    private const int IdLength = 36;
    private const int ShortTextLength = 64;
    private const int MediumTextLength = 128;
    private const int LongTextLength = 256;
    private const int UrlLength = 2048;

    public static ModelBuilder ApplyInitialSchema(this ModelBuilder modelBuilder)
    {
        ConfigureProjects(modelBuilder);
        ConfigureRepositories(modelBuilder);
        ConfigureWorkflowProfiles(modelBuilder);
        ConfigureSymphonyReleaseArtifacts(modelBuilder);
        ConfigureSymphonyInstances(modelBuilder);
        ConfigureInstanceSnapshots(modelBuilder);
        ConfigureTrackedIssues(modelBuilder);
        ConfigureRuns(modelBuilder);
        ConfigureRunAttempts(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureAlerts(modelBuilder);
        ConfigureReports(modelBuilder);
        ConfigureSecretDescriptors(modelBuilder);
        ConfigureAuditEvents(modelBuilder);
        ConfigureBackgroundOperations(modelBuilder);

        return modelBuilder;
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectRecord>(builder =>
        {
            builder.ToTable("Projects");
            builder.HasKey(project => project.Id);

            builder.Property(project => project.Id).HasMaxLength(IdLength);
            builder.Property(project => project.Name).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(project => project.OwnerName).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(project => project.Status).HasMaxLength(ShortTextLength).IsRequired();
        });
    }

    private static void ConfigureRepositories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RepositoryRecord>(builder =>
        {
            builder.ToTable("Repositories");
            builder.HasKey(repository => repository.Id);

            builder.Property(repository => repository.Id).HasMaxLength(IdLength);
            builder.Property(repository => repository.ProjectId).HasMaxLength(IdLength);
            builder.Property(repository => repository.Provider).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(repository => repository.Owner).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(repository => repository.Name).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(repository => repository.DefaultBranch).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(repository => repository.CloneUrl).HasMaxLength(UrlLength).IsRequired();
            builder.Property(repository => repository.WebUrl).HasMaxLength(UrlLength).IsRequired();

            builder.HasOne<ProjectRecord>()
                .WithMany()
                .HasForeignKey(repository => repository.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(repository => repository.ProjectId);
            builder.HasIndex(repository => new { repository.Provider, repository.Owner, repository.Name }).IsUnique();
        });
    }

    private static void ConfigureWorkflowProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowProfileRecord>(builder =>
        {
            builder.ToTable("WorkflowProfiles");
            builder.HasKey(profile => profile.Id);

            builder.Property(profile => profile.Id).HasMaxLength(IdLength);
            builder.Property(profile => profile.Name).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(profile => profile.Description).HasMaxLength(LongTextLength);
            builder.Property(profile => profile.ExecutionMode).HasMaxLength(ShortTextLength);
            builder.Property(profile => profile.WorkflowSource).HasColumnType("TEXT").IsRequired();
        });
    }

    private static void ConfigureSymphonyReleaseArtifacts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SymphonyReleaseArtifactRecord>(builder =>
        {
            builder.ToTable("SymphonyReleaseArtifacts");
            builder.HasKey(artifact => artifact.Id);

            builder.Property(artifact => artifact.Id).HasMaxLength(IdLength);
            builder.Property(artifact => artifact.ReleaseTag).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(artifact => artifact.AssetName).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(artifact => artifact.TargetRuntime).HasMaxLength(ShortTextLength);
            builder.Property(artifact => artifact.SourceUrl).HasMaxLength(UrlLength).IsRequired();
            builder.Property(artifact => artifact.LocalPath).HasMaxLength(UrlLength);
            builder.Property(artifact => artifact.Checksum).HasMaxLength(MediumTextLength);
            builder.Property(artifact => artifact.MetadataJson).HasColumnType("TEXT");

            builder.HasIndex(artifact => new { artifact.ReleaseTag, artifact.AssetName }).IsUnique();
        });
    }

    private static void ConfigureSymphonyInstances(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SymphonyInstanceRecord>(builder =>
        {
            builder.ToTable("SymphonyInstances");
            builder.HasKey(instance => instance.Id);

            builder.Property(instance => instance.Id).HasMaxLength(IdLength);
            builder.Property(instance => instance.RepositoryId).HasMaxLength(IdLength).IsRequired();
            builder.Property(instance => instance.WorkflowProfileId).HasMaxLength(IdLength);
            builder.Property(instance => instance.DisplayName).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(instance => instance.ExecutionMode).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(instance => instance.BaseUrl).HasMaxLength(UrlLength).IsRequired();
            builder.Property(instance => instance.Status).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(instance => instance.HealthStatus).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(instance => instance.DeliveryStatus).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(instance => instance.ReleaseSelector).HasMaxLength(MediumTextLength);
            builder.Property(instance => instance.ResolvedReleaseTag).HasMaxLength(MediumTextLength);
            builder.Property(instance => instance.GitHubSecretId).HasMaxLength(IdLength);
            builder.Property(instance => instance.OpenAiSecretId).HasMaxLength(IdLength);

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(instance => instance.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<WorkflowProfileRecord>()
                .WithMany()
                .HasForeignKey(instance => instance.WorkflowProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(instance => instance.RepositoryId);
            builder.HasIndex(instance => new { instance.Status, instance.HealthStatus });
            builder.HasIndex(instance => instance.ExecutionMode);
        });
    }

    private static void ConfigureInstanceSnapshots(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstanceSnapshotRecord>(builder =>
        {
            builder.ToTable("InstanceSnapshots");
            builder.HasKey(snapshot => snapshot.Id);

            builder.Property(snapshot => snapshot.Id).HasMaxLength(IdLength);
            builder.Property(snapshot => snapshot.SymphonyInstanceId).HasMaxLength(IdLength).IsRequired();
            builder.Property(snapshot => snapshot.HealthStatus).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(snapshot => snapshot.ErrorMessage).HasMaxLength(LongTextLength);
            builder.Property(snapshot => snapshot.HealthJson).HasColumnType("TEXT");
            builder.Property(snapshot => snapshot.RuntimeJson).HasColumnType("TEXT");
            builder.Property(snapshot => snapshot.StateJson).HasColumnType("TEXT");

            builder.HasOne<SymphonyInstanceRecord>()
                .WithMany()
                .HasForeignKey(snapshot => snapshot.SymphonyInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(snapshot => new { snapshot.SymphonyInstanceId, snapshot.CapturedAtUtc });
        });
    }

    private static void ConfigureTrackedIssues(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackedIssueRecord>(builder =>
        {
            builder.ToTable("TrackedIssues");
            builder.HasKey(issue => issue.Id);

            builder.Property(issue => issue.Id).HasMaxLength(IdLength);
            builder.Property(issue => issue.RepositoryId).HasMaxLength(IdLength).IsRequired();
            builder.Property(issue => issue.Title).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(issue => issue.SymphonyStatus).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(issue => issue.Url).HasMaxLength(UrlLength);
            builder.Property(issue => issue.LabelsJson).HasColumnType("TEXT");
            builder.Property(issue => issue.AssigneesJson).HasColumnType("TEXT");
            builder.Property(issue => issue.PullRequestsJson).HasColumnType("TEXT");

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(issue => issue.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(issue => new { issue.RepositoryId, issue.GitHubIssueNumber });
            builder.HasIndex(issue => new { issue.RepositoryId, issue.SymphonyStatus, issue.IsBlocked });
        });
    }

    private static void ConfigureRuns(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RunRecord>(builder =>
        {
            builder.ToTable("Runs");
            builder.HasKey(run => run.Id);

            builder.Property(run => run.Id).HasMaxLength(IdLength);
            builder.Property(run => run.SymphonyInstanceId).HasMaxLength(IdLength).IsRequired();
            builder.Property(run => run.RepositoryId).HasMaxLength(IdLength).IsRequired();
            builder.Property(run => run.TrackedIssueId).HasMaxLength(IdLength);
            builder.Property(run => run.Status).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(run => run.BranchName).HasMaxLength(MediumTextLength);
            builder.Property(run => run.PullRequestUrl).HasMaxLength(UrlLength);
            builder.Property(run => run.Summary).HasMaxLength(LongTextLength);

            builder.HasOne<SymphonyInstanceRecord>()
                .WithMany()
                .HasForeignKey(run => run.SymphonyInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(run => run.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<TrackedIssueRecord>()
                .WithMany()
                .HasForeignKey(run => run.TrackedIssueId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(run => new { run.SymphonyInstanceId, run.Status, run.StartedAtUtc });
            builder.HasIndex(run => new { run.RepositoryId, run.GitHubIssueNumber });
        });
    }

    private static void ConfigureRunAttempts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RunAttemptRecord>(builder =>
        {
            builder.ToTable("RunAttempts");
            builder.HasKey(attempt => attempt.Id);

            builder.Property(attempt => attempt.Id).HasMaxLength(IdLength);
            builder.Property(attempt => attempt.RunId).HasMaxLength(IdLength).IsRequired();
            builder.Property(attempt => attempt.Status).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(attempt => attempt.ErrorMessage).HasMaxLength(LongTextLength);
            builder.Property(attempt => attempt.LogPath).HasMaxLength(UrlLength);

            builder.HasOne<RunRecord>()
                .WithMany()
                .HasForeignKey(attempt => attempt.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(attempt => new { attempt.RunId, attempt.AttemptNumber }).IsUnique();
        });
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventRecord>(builder =>
        {
            builder.ToTable("Events");
            builder.HasKey(eventRecord => eventRecord.Id);

            builder.Property(eventRecord => eventRecord.Id).HasMaxLength(IdLength);
            builder.Property(eventRecord => eventRecord.SymphonyInstanceId).HasMaxLength(IdLength);
            builder.Property(eventRecord => eventRecord.RepositoryId).HasMaxLength(IdLength);
            builder.Property(eventRecord => eventRecord.EventType).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(eventRecord => eventRecord.Severity).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(eventRecord => eventRecord.Message).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(eventRecord => eventRecord.PayloadJson).HasColumnType("TEXT");

            builder.HasOne<SymphonyInstanceRecord>()
                .WithMany()
                .HasForeignKey(eventRecord => eventRecord.SymphonyInstanceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(eventRecord => eventRecord.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(eventRecord => new { eventRecord.SymphonyInstanceId, eventRecord.OccurredAtUtc });
            builder.HasIndex(eventRecord => new { eventRecord.RepositoryId, eventRecord.OccurredAtUtc });
        });
    }

    private static void ConfigureAlerts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlertRecord>(builder =>
        {
            builder.ToTable("Alerts");
            builder.HasKey(alert => alert.Id);

            builder.Property(alert => alert.Id).HasMaxLength(IdLength);
            builder.Property(alert => alert.SymphonyInstanceId).HasMaxLength(IdLength);
            builder.Property(alert => alert.RepositoryId).HasMaxLength(IdLength);
            builder.Property(alert => alert.TrackedIssueId).HasMaxLength(IdLength);
            builder.Property(alert => alert.Status).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(alert => alert.Severity).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(alert => alert.Title).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(alert => alert.Message).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(alert => alert.MetadataJson).HasColumnType("TEXT");

            builder.HasOne<SymphonyInstanceRecord>()
                .WithMany()
                .HasForeignKey(alert => alert.SymphonyInstanceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(alert => alert.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<TrackedIssueRecord>()
                .WithMany()
                .HasForeignKey(alert => alert.TrackedIssueId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(alert => new { alert.Status, alert.Severity, alert.CreatedAtUtc });
        });
    }

    private static void ConfigureReports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportRecord>(builder =>
        {
            builder.ToTable("Reports");
            builder.HasKey(report => report.Id);

            builder.Property(report => report.Id).HasMaxLength(IdLength);
            builder.Property(report => report.ProjectId).HasMaxLength(IdLength);
            builder.Property(report => report.RepositoryId).HasMaxLength(IdLength);
            builder.Property(report => report.ReportType).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(report => report.Title).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(report => report.GeneratedByUserId).HasMaxLength(MediumTextLength);
            builder.Property(report => report.ContentMarkdown).HasColumnType("TEXT");
            builder.Property(report => report.ContentHtml).HasColumnType("TEXT");
            builder.Property(report => report.MetadataJson).HasColumnType("TEXT");

            builder.HasOne<ProjectRecord>()
                .WithMany()
                .HasForeignKey(report => report.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(report => report.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(report => new { report.ReportType, report.GeneratedAtUtc });
        });
    }

    private static void ConfigureSecretDescriptors(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecretDescriptorRecord>(builder =>
        {
            builder.ToTable("SecretDescriptors");
            builder.HasKey(secret => secret.Id);

            builder.Property(secret => secret.Id).HasMaxLength(IdLength);
            builder.Property(secret => secret.ScopeType).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(secret => secret.ScopeId).HasMaxLength(MediumTextLength);
            builder.Property(secret => secret.SecretType).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(secret => secret.DisplayName).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(secret => secret.StorageKey).HasMaxLength(LongTextLength).IsRequired();
            builder.Property(secret => secret.CreatedByUserId).HasMaxLength(MediumTextLength);

            builder.HasIndex(secret => new { secret.ScopeType, secret.ScopeId, secret.SecretType });
        });
    }

    private static void ConfigureAuditEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEventRecord>(builder =>
        {
            builder.ToTable("AuditEvents");
            builder.HasKey(auditEvent => auditEvent.Id);

            builder.Property(auditEvent => auditEvent.Id).HasMaxLength(IdLength);
            builder.Property(auditEvent => auditEvent.ActorUserId).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(auditEvent => auditEvent.Action).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(auditEvent => auditEvent.ResourceType).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(auditEvent => auditEvent.ResourceId).HasMaxLength(MediumTextLength);
            builder.Property(auditEvent => auditEvent.MetadataJson).HasColumnType("TEXT");

            builder.HasIndex(auditEvent => new { auditEvent.ActorUserId, auditEvent.OccurredAtUtc });
        });
    }

    private static void ConfigureBackgroundOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackgroundOperationRecord>(builder =>
        {
            builder.ToTable("BackgroundOperations");
            builder.HasKey(operation => operation.Id);

            builder.Property(operation => operation.Id).HasMaxLength(IdLength);
            builder.Property(operation => operation.SymphonyInstanceId).HasMaxLength(IdLength);
            builder.Property(operation => operation.RepositoryId).HasMaxLength(IdLength);
            builder.Property(operation => operation.OperationType).HasMaxLength(MediumTextLength).IsRequired();
            builder.Property(operation => operation.Status).HasMaxLength(ShortTextLength).IsRequired();
            builder.Property(operation => operation.RequestedByUserId).HasMaxLength(MediumTextLength);
            builder.Property(operation => operation.ErrorMessage).HasMaxLength(LongTextLength);
            builder.Property(operation => operation.PayloadJson).HasColumnType("TEXT");

            builder.HasOne<SymphonyInstanceRecord>()
                .WithMany()
                .HasForeignKey(operation => operation.SymphonyInstanceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<RepositoryRecord>()
                .WithMany()
                .HasForeignKey(operation => operation.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(operation => new { operation.Status, operation.CreatedAtUtc });
        });
    }
}
