using Conductor.Core.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class WorkflowProfileConfiguration : IEntityTypeConfiguration<WorkflowProfile>
{
    public void Configure(EntityTypeBuilder<WorkflowProfile> builder)
    {
        builder.ToTable("WorkflowProfiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(StronglyTypedIdValueConverters.WorkflowProfileId)
            .ValueGeneratedNever();

        builder.Property(profile => profile.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(profile => profile.Description)
            .HasMaxLength(500);

        builder.Property(profile => profile.WorkflowSource)
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(profile => profile.IsDefault)
            .IsRequired();

        builder.Property(profile => profile.Revision)
            .IsRequired();

        builder.Property(profile => profile.CreatedAtUtc)
            .IsRequired();

        builder.Property(profile => profile.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(profile => profile.IsDefault);
    }
}
