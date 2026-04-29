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

        builder.HasKey(snapshot => new { snapshot.SymphonyInstanceId, snapshot.CapturedAtUtc });

        builder.Property(snapshot => snapshot.SymphonyInstanceId)
            .HasConversion(StronglyTypedIdValueConverters.SymphonyInstanceId)
            .IsRequired();

        builder.Property(snapshot => snapshot.HealthStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(snapshot => snapshot.HealthJson)
            .HasColumnType("TEXT");

        builder.Property(snapshot => snapshot.RuntimeJson)
            .HasColumnType("TEXT");

        builder.Property(snapshot => snapshot.StateJson)
            .HasColumnType("TEXT");

        builder.HasIndex(snapshot => new { snapshot.SymphonyInstanceId, snapshot.CapturedAtUtc });

        builder.HasOne<SymphonyInstance>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.SymphonyInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
