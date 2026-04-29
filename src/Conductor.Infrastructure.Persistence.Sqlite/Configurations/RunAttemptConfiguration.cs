using Conductor.Core.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class RunAttemptConfiguration : IEntityTypeConfiguration<RunAttempt>
{
    public void Configure(EntityTypeBuilder<RunAttempt> builder)
    {
        builder.ToTable("RunAttempts");

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Id)
            .HasConversion(StronglyTypedIdValueConverters.RunAttemptId)
            .ValueGeneratedNever();

        builder.Property(attempt => attempt.RunId)
            .HasConversion(StronglyTypedIdValueConverters.RunId)
            .IsRequired();

        builder.Property(attempt => attempt.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(attempt => attempt.StartedAtUtc)
            .IsRequired();

        builder.Property(attempt => attempt.FinishedAtUtc);

        builder.Property(attempt => attempt.ExitReason)
            .HasMaxLength(500);

        builder.Property(attempt => attempt.ErrorDetail)
            .HasColumnType("TEXT");

        builder.HasIndex(attempt => new { attempt.RunId, attempt.AttemptNumber })
            .IsUnique();

        builder.HasOne<Run>()
            .WithMany()
            .HasForeignKey(attempt => attempt.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
