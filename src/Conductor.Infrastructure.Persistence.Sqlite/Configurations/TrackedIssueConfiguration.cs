using Conductor.Core.Domain.Issues;
using Conductor.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class TrackedIssueConfiguration : IEntityTypeConfiguration<TrackedIssue>
{
    public void Configure(EntityTypeBuilder<TrackedIssue> builder)
    {
        builder.ToTable("TrackedIssues");

        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.Id)
            .HasConversion(StronglyTypedIdValueConverters.TrackedIssueId)
            .ValueGeneratedNever();

        builder.Property(issue => issue.RepositoryId)
            .HasConversion(StronglyTypedIdValueConverters.RepositoryId)
            .IsRequired();

        builder.Property(issue => issue.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(issue => issue.State)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(issue => issue.LabelsJson)
            .HasColumnType("TEXT");

        builder.Property(issue => issue.Milestone)
            .HasMaxLength(255);

        builder.Property(issue => issue.AssigneeLoginsJson)
            .HasColumnType("TEXT");

        builder.Property(issue => issue.Url)
            .HasConversion(StronglyTypedIdValueConverters.AbsoluteUri)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(issue => issue.SymphonyStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(issue => issue.LastRunStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(issue => issue.LastActivityAtUtc)
            .IsRequired();

        builder.Property(issue => issue.IsBlocked)
            .IsRequired();

        builder.Property(issue => issue.BlockerReason)
            .HasMaxLength(1000);

        builder.HasIndex(issue => new { issue.RepositoryId, issue.GitHubIssueNumber });
        builder.HasIndex(issue => new { issue.RepositoryId, issue.SymphonyStatus, issue.IsBlocked });

        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(issue => issue.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
