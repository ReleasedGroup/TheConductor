using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Abstractions.Reporting;

public interface IReportRenderer
{
    string Format { get; }

    Task<RenderedReport> RenderAsync(ReportRequest request, CancellationToken cancellationToken);
}

public sealed record ReportRequest(
    ReportId ReportId,
    string Title,
    string MarkdownContent,
    DateTimeOffset GeneratedAtUtc);

public sealed record RenderedReport(
    ReportId ReportId,
    string Format,
    string Content,
    string ContentType);
