using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class SymphonyInstanceConfiguration : IEntityTypeConfiguration<SymphonyInstance>
{
    public void Configure(EntityTypeBuilder<SymphonyInstance> builder)
    {
        builder.ToTable("SymphonyInstances");

        builder.HasKey(instance => instance.Id);

        builder.Property(instance => instance.Id)
            .HasConversion(StronglyTypedIdValueConverters.SymphonyInstanceId)
            .ValueGeneratedNever();

        builder.Property(instance => instance.RepositoryId)
            .HasConversion(StronglyTypedIdValueConverters.RepositoryId)
            .IsRequired();

        builder.Property(instance => instance.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(instance => instance.ExecutionMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(instance => instance.BaseUrl)
            .HasConversion(StronglyTypedIdValueConverters.AbsoluteUri)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(instance => instance.LifecycleStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(instance => instance.HealthStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(instance => instance.RepositoryId);
        builder.HasIndex(instance => new { instance.LifecycleStatus, instance.HealthStatus });
        builder.HasIndex(instance => instance.ExecutionMode);

        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(instance => instance.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
