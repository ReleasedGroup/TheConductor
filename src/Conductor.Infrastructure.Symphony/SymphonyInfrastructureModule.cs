using Conductor.Core.Application;

namespace Conductor.Infrastructure.Symphony;

public static class SymphonyInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Symphony",
        "Typed HTTP clients and contracts for Symphony runtime APIs.");
}
