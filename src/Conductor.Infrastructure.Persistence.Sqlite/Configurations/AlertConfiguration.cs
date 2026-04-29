using Conductor.Core.Domain.Alerts;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("Alerts");

        builder.HasKey(alert => alert.Id);

        builder.Property(alert => alert.Id)
            .HasConversion(StronglyTypedIdValueConverters.AlertId)
            .ValueGeneratedNever();

        builder.Property(alert => alert.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(alert => alert.Severity)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(alert => alert.Source)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(alert => alert.Summary)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(alert => alert.RecommendedAction)
            .HasMaxLength(1000);

        builder.Property(alert => alert.ResolutionNote)
            .HasMaxLength(1000);

        builder.Property(alert => alert.SymphonyInstanceId)
            .HasConversion(StronglyTypedIdValueConverters.NullableSymphonyInstanceId);

        builder.Property(alert => alert.RepositoryId)
            .HasConversion(StronglyTypedIdValueConverters.NullableRepositoryId);

        builder.Property(alert => alert.RunId)
            .HasConversion(StronglyTypedIdValueConverters.NullableRunId);

        builder.Property(alert => alert.GitHubIssueNumber);

        builder.HasIndex(alert => new { alert.Status, alert.Severity, alert.CreatedAtUtc });

        builder.HasOne<SymphonyInstance>()
            .WithMany()
            .HasForeignKey(alert => alert.SymphonyInstanceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(alert => alert.RepositoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Run>()
            .WithMany()
            .HasForeignKey(alert => alert.RunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
