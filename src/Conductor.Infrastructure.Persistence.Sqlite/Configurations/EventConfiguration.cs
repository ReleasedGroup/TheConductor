using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainEvent = Conductor.Core.Domain.Events.Event;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<DomainEvent>
{
    public void Configure(EntityTypeBuilder<DomainEvent> builder)
    {
        builder.ToTable("Events");

        builder.HasKey(recordedEvent => recordedEvent.Id);

        builder.Property(recordedEvent => recordedEvent.Id)
            .HasConversion(StronglyTypedIdValueConverters.EventId)
            .ValueGeneratedNever();

        builder.Property(recordedEvent => recordedEvent.SymphonyInstanceId)
            .HasConversion(StronglyTypedIdValueConverters.NullableSymphonyInstanceId);

        builder.Property(recordedEvent => recordedEvent.RepositoryId)
            .HasConversion(StronglyTypedIdValueConverters.NullableRepositoryId);

        builder.Property(recordedEvent => recordedEvent.IssueNumber);

        builder.Property(recordedEvent => recordedEvent.Severity)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(recordedEvent => recordedEvent.EventType)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(recordedEvent => recordedEvent.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(recordedEvent => recordedEvent.PayloadJson)
            .HasColumnType("TEXT");

        builder.HasIndex(recordedEvent => new { recordedEvent.SymphonyInstanceId, recordedEvent.OccurredAtUtc });
        builder.HasIndex(recordedEvent => new { recordedEvent.RepositoryId, recordedEvent.OccurredAtUtc });

        builder.HasOne<SymphonyInstance>()
            .WithMany()
            .HasForeignKey(recordedEvent => recordedEvent.SymphonyInstanceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(recordedEvent => recordedEvent.RepositoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
