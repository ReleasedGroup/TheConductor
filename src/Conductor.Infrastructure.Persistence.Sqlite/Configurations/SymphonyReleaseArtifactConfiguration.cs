using Conductor.Core.Domain.SymphonyReleases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class SymphonyReleaseArtifactConfiguration : IEntityTypeConfiguration<SymphonyReleaseArtifact>
{
    public void Configure(EntityTypeBuilder<SymphonyReleaseArtifact> builder)
    {
        builder.ToTable("SymphonyReleaseArtifacts");

        builder.HasKey(artifact => new { artifact.ReleaseTag, artifact.AssetName });

        builder.Property(artifact => artifact.ReleaseTag)
            .HasConversion(StronglyTypedIdValueConverters.ReleaseTag)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(artifact => artifact.AssetName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(artifact => artifact.SourceUrl)
            .HasConversion(StronglyTypedIdValueConverters.AbsoluteUri)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(artifact => artifact.DownloadedAtUtc)
            .IsRequired();

        builder.Property(artifact => artifact.Checksum)
            .HasMaxLength(256);

        builder.HasIndex(artifact => artifact.ReleaseTag);
    }
}
