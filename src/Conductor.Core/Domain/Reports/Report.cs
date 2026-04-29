using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Reports;

public sealed class Report
{
    public Report(
        ReportId id,
        ReportType reportType,
        string scope,
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        DateTimeOffset generatedAtUtc,
        string markdown,
        string html,
        string? pdfPath,
        string? metadataJson)
    {
        if (periodEndUtc < periodStartUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(periodEndUtc), "Report period cannot end before it starts.");
        }

        Id = id;
        ReportType = reportType;
        Scope = Guard.NotWhiteSpace(scope, nameof(scope));
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        GeneratedAtUtc = generatedAtUtc;
        Markdown = Guard.NotWhiteSpace(markdown, nameof(markdown));
        Html = Guard.NotWhiteSpace(html, nameof(html));
        PdfPath = Guard.OptionalTrimmed(pdfPath);
        MetadataJson = Guard.OptionalTrimmed(metadataJson);
    }

    public ReportId Id { get; }

    public ReportType ReportType { get; }

    public string Scope { get; }

    public DateTimeOffset PeriodStartUtc { get; }

    public DateTimeOffset PeriodEndUtc { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public string Markdown { get; }

    public string Html { get; }

    public string? PdfPath { get; }

    public string? MetadataJson { get; }
}
