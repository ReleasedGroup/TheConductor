using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories");

        builder.HasKey(repository => repository.Id);

        builder.Property(repository => repository.Id)
            .HasConversion(StronglyTypedIdValueConverters.RepositoryId)
            .ValueGeneratedNever();

        builder.Property(repository => repository.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(repository => repository.Owner)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(repository => repository.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Ignore(repository => repository.FullName);

        builder.Property(repository => repository.DefaultBranch)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(repository => repository.CloneUrl)
            .HasConversion(StronglyTypedIdValueConverters.AbsoluteUri)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(repository => repository.WebUrl)
            .HasConversion(StronglyTypedIdValueConverters.AbsoluteUri)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(repository => repository.IsArchived)
            .IsRequired();

        builder.Property(repository => repository.ProjectId)
            .HasConversion(StronglyTypedIdValueConverters.NullableProjectId);

        builder.HasIndex(repository => repository.ProjectId);

        builder.HasIndex(repository => new
        {
            repository.Provider,
            repository.Owner,
            repository.Name,
        })
            .IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(repository => repository.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
