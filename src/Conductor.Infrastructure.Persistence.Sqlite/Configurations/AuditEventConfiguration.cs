using Conductor.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.Id)
            .HasConversion(StronglyTypedIdValueConverters.AuditEventId)
            .ValueGeneratedNever();

        builder.Property(auditEvent => auditEvent.ActorUserId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.Action)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.TargetResourceType)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.TargetResourceId)
            .HasMaxLength(128);

        builder.Property(auditEvent => auditEvent.Outcome)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.CorrelationId)
            .HasMaxLength(128);

        builder.Property(auditEvent => auditEvent.Message)
            .HasMaxLength(1000);

        builder.Property(auditEvent => auditEvent.MetadataJson)
            .HasColumnType("TEXT");

        builder.HasIndex(auditEvent => new { auditEvent.ActorUserId, auditEvent.OccurredAtUtc });
    }
}
