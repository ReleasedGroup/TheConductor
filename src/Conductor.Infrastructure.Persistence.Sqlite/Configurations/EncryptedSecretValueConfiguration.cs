using Conductor.Core.Domain.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class EncryptedSecretValueConfiguration : IEntityTypeConfiguration<EncryptedSecretValue>
{
    public void Configure(EntityTypeBuilder<EncryptedSecretValue> builder)
    {
        builder.ToTable("EncryptedSecretValues");

        builder.HasKey(secret => secret.SecretId);

        builder.Property(secret => secret.SecretId)
            .HasConversion(StronglyTypedIdValueConverters.SecretId)
            .ValueGeneratedNever();

        builder.Property(secret => secret.ProtectedValue)
            .IsRequired();

        builder.Property(secret => secret.CreatedAtUtc)
            .IsRequired();

        builder.Property(secret => secret.RotatedAtUtc);

        builder.HasOne<SecretDescriptor>()
            .WithOne()
            .HasForeignKey<EncryptedSecretValue>(secret => secret.SecretId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
