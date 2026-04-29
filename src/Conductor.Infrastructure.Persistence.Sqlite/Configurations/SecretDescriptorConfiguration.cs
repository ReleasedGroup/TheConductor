using Conductor.Core.Domain.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class SecretDescriptorConfiguration : IEntityTypeConfiguration<SecretDescriptor>
{
    public void Configure(EntityTypeBuilder<SecretDescriptor> builder)
    {
        builder.ToTable("SecretDescriptors");

        builder.HasKey(secret => secret.Id);

        builder.Property(secret => secret.Id)
            .HasConversion(StronglyTypedIdValueConverters.SecretId)
            .ValueGeneratedNever();

        builder.Property(secret => secret.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(secret => secret.SecretType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(secret => secret.ScopeType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(secret => secret.ScopeId)
            .HasMaxLength(128);

        builder.Property(secret => secret.CreatedAtUtc)
            .IsRequired();

        builder.Property(secret => secret.RotatedAtUtc);

        builder.Property(secret => secret.ValidationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(secret => secret.ValidatedAtUtc);

        builder.Property(secret => secret.ValidationMessage)
            .HasMaxLength(500);

        builder.Property(secret => secret.ValidationMetadataJson);

        builder.HasIndex(secret => new
        {
            secret.ScopeType,
            secret.ScopeId,
            secret.SecretType,
        });
    }
}
