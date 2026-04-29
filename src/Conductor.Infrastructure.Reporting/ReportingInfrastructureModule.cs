using Conductor.Core.Application;

namespace Conductor.Infrastructure.Reporting;

public static class ReportingInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Reporting",
        "Markdown, HTML, and future PDF report rendering adapters.");
}
