using Conductor.Core.Application;

namespace Conductor.Infrastructure.Runners.Docker;

public static class DockerRunnerInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Runners.Docker",
        "Docker-backed Symphony lifecycle, log, and volume management adapters.");
}
