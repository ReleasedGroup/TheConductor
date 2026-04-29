using Conductor.Core.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class BackgroundOperationConfiguration : IEntityTypeConfiguration<BackgroundOperation>
{
    public void Configure(EntityTypeBuilder<BackgroundOperation> builder)
    {
        builder.ToTable("BackgroundOperations");

        builder.HasKey(operation => operation.Id);

        builder.Property(operation => operation.Id)
            .HasConversion(StronglyTypedIdValueConverters.BackgroundOperationId)
            .ValueGeneratedNever();

        builder.Property(operation => operation.OperationType)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(operation => operation.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(operation => operation.StartedAtUtc);

        builder.Property(operation => operation.CompletedAtUtc);

        builder.Property(operation => operation.TargetResourceType)
            .HasMaxLength(120);

        builder.Property(operation => operation.TargetResourceId)
            .HasMaxLength(128);

        builder.Property(operation => operation.CorrelationId)
            .HasMaxLength(128);

        builder.Property(operation => operation.Summary)
            .HasMaxLength(1000);

        builder.Property(operation => operation.ErrorDetail)
            .HasColumnType("TEXT");

        builder.HasIndex(operation => new { operation.Status, operation.CreatedAtUtc });
    }
}
