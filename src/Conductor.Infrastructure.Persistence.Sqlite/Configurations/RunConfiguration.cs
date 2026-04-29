using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Runs;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.ToTable("Runs");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.Id)
            .HasConversion(StronglyTypedIdValueConverters.RunId)
            .ValueGeneratedNever();

        builder.Property(run => run.SymphonyInstanceId)
            .HasConversion(StronglyTypedIdValueConverters.SymphonyInstanceId)
            .IsRequired();

        builder.Property(run => run.RepositoryId)
            .HasConversion(StronglyTypedIdValueConverters.RepositoryId)
            .IsRequired();

        builder.Property(run => run.SymphonyRunId)
            .HasMaxLength(128);

        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(run => run.StartedAtUtc)
            .IsRequired();

        builder.Property(run => run.FinishedAtUtc);

        builder.Property(run => run.AttemptCount)
            .IsRequired();

        builder.Property(run => run.TokenInput)
            .IsRequired();

        builder.Property(run => run.TokenOutput)
            .IsRequired();

        builder.Property(run => run.ErrorSummary)
            .HasMaxLength(1000);

        builder.Property(run => run.BranchName)
            .HasMaxLength(255);

        builder.Property(run => run.PullRequestUrl)
            .HasConversion(StronglyTypedIdValueConverters.NullableAbsoluteUri)
            .HasMaxLength(2048);

        builder.Ignore(run => run.TokenTotal);

        builder.HasIndex(run => new { run.SymphonyInstanceId, run.Status, run.StartedAtUtc });
        builder.HasIndex(run => new { run.RepositoryId, run.GitHubIssueNumber });

        builder.HasOne<SymphonyInstance>()
            .WithMany()
            .HasForeignKey(run => run.SymphonyInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(run => run.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
