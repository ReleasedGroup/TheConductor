using Conductor.Core.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasConversion(StronglyTypedIdValueConverters.ReportId)
            .ValueGeneratedNever();

        builder.Property(report => report.ReportType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(report => report.Scope)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(report => report.PeriodStartUtc)
            .IsRequired();

        builder.Property(report => report.PeriodEndUtc)
            .IsRequired();

        builder.Property(report => report.GeneratedAtUtc)
            .IsRequired();

        builder.Property(report => report.Markdown)
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(report => report.Html)
            .HasColumnType("TEXT")
            .IsRequired();

        builder.Property(report => report.PdfPath)
            .HasMaxLength(1024);

        builder.Property(report => report.MetadataJson)
            .HasColumnType("TEXT");

        builder.HasIndex(report => new { report.ReportType, report.GeneratedAtUtc });
    }
}
