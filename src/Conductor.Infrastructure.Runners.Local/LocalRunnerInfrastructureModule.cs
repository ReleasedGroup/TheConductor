using Conductor.Core.Application;

namespace Conductor.Infrastructure.Runners.Local;

public static class LocalRunnerInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Runners.Local",
        "Local-process Symphony lifecycle, process, and log management adapters.");
}
