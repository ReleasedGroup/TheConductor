using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.Workflows;
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

        builder.Property(instance => instance.Port);

        builder.Property(instance => instance.ContainerName)
            .HasMaxLength(128);

        builder.Property(instance => instance.AzureResourceId)
            .HasMaxLength(512);

        builder.Property(instance => instance.LifecycleStatus)
            .HasConversion<string>()
            .HasColumnName("Status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(instance => instance.HealthStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(instance => instance.SymphonyVersion)
            .HasMaxLength(64);

        builder.Property(instance => instance.SymphonyReleaseTag)
            .HasMaxLength(128);

        builder.Property(instance => instance.SymphonyArtifactSourceUrl)
            .HasConversion(StronglyTypedIdValueConverters.NullableAbsoluteUri)
            .HasMaxLength(2048);

        builder.Property(instance => instance.SymphonyArtifactChecksum)
            .HasMaxLength(256);

        builder.Property(instance => instance.GitHubCredentialSecretId)
            .HasConversion(StronglyTypedIdValueConverters.NullableSecretId);

        builder.Property(instance => instance.GitHubCredentialInheritanceMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(instance => instance.OpenAiCredentialSecretId)
            .HasConversion(StronglyTypedIdValueConverters.NullableSecretId);

        builder.Property(instance => instance.OpenAiCredentialInheritanceMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(instance => instance.WorkflowPath)
            .HasMaxLength(1024);

        builder.Property(instance => instance.DataPath)
            .HasMaxLength(1024);

        builder.Property(instance => instance.WorkflowProfileId)
            .HasConversion(StronglyTypedIdValueConverters.NullableWorkflowProfileId);

        builder.Property(instance => instance.CreatedAtUtc)
            .IsRequired();

        builder.Property(instance => instance.LastStartedAtUtc);

        builder.Property(instance => instance.LastHealthCheckAtUtc);

        builder.Property(instance => instance.LastSeenAtUtc);

        builder.HasIndex(instance => instance.RepositoryId);
        builder.HasIndex(instance => new { instance.LifecycleStatus, instance.HealthStatus });
        builder.HasIndex(instance => instance.ExecutionMode);
        builder.HasIndex(instance => instance.WorkflowProfileId);

        builder.HasOne<Repository>()
            .WithMany()
            .HasForeignKey(instance => instance.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SecretDescriptor>()
            .WithMany()
            .HasForeignKey(instance => instance.GitHubCredentialSecretId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SecretDescriptor>()
            .WithMany()
            .HasForeignKey(instance => instance.OpenAiCredentialSecretId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<WorkflowProfile>()
            .WithMany()
            .HasForeignKey(instance => instance.WorkflowProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
