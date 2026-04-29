using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class InstanceSnapshotConfiguration : IEntityTypeConfiguration<InstanceSnapshot>
{
    public void Configure(EntityTypeBuilder<InstanceSnapshot> builder)
    {
        builder.ToTable("InstanceSnapshots");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id)
            .HasConversion(StronglyTypedIdValueConverters.InstanceSnapshotId)
            .ValueGeneratedNever();

        builder.Property(snapshot => snapshot.SymphonyInstanceId)
            .HasConversion(StronglyTypedIdValueConverters.SymphonyInstanceId)
            .IsRequired();

        builder.Property(snapshot => snapshot.HealthStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(snapshot => snapshot.HttpStatusCode);

        builder.Property(snapshot => snapshot.LatencyMilliseconds);

        builder.Property(snapshot => snapshot.ErrorMessage)
            .HasMaxLength(255);

        builder.Property(snapshot => snapshot.HealthJson)
            .HasColumnType("TEXT");

        builder.Property(snapshot => snapshot.RuntimeJson)
            .HasColumnType("TEXT");

        builder.Property(snapshot => snapshot.StateJson)
            .HasColumnType("TEXT");

        builder.Property(snapshot => snapshot.ApplicationName)
            .HasMaxLength(128);

        builder.Property(snapshot => snapshot.ApplicationVersion)
            .HasMaxLength(128);

        builder.Property(snapshot => snapshot.RuntimeInstanceId)
            .HasMaxLength(128);

        builder.Property(snapshot => snapshot.WorkflowOwner)
            .HasMaxLength(128);

        builder.Property(snapshot => snapshot.WorkflowRepository)
            .HasMaxLength(128);

        builder.Property(snapshot => snapshot.WorkflowSourcePath)
            .HasMaxLength(2048);

        builder.Property(snapshot => snapshot.PersistenceProvider)
            .HasMaxLength(128);

        builder.Property(snapshot => snapshot.RuntimeDefaultsJson)
            .HasColumnType("TEXT");

        builder.Property(snapshot => snapshot.ActiveIssueCount)
            .IsRequired();

        builder.Property(snapshot => snapshot.RunningSessionCount)
            .IsRequired();

        builder.Property(snapshot => snapshot.RetryQueueCount)
            .IsRequired();

        builder.Property(snapshot => snapshot.FailedRunCount)
            .IsRequired();

        builder.Property(snapshot => snapshot.TokenInputTotal)
            .IsRequired();

        builder.Property(snapshot => snapshot.TokenOutputTotal)
            .IsRequired();

        builder.Ignore(snapshot => snapshot.TokenTotal);

        builder.HasIndex(snapshot => new { snapshot.SymphonyInstanceId, snapshot.CapturedAtUtc });

        builder.HasOne<SymphonyInstance>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.SymphonyInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
