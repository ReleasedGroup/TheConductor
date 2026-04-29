using Conductor.Core.Application;

namespace Conductor.Infrastructure.Secrets;

public static class SecretsInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Secrets",
        "Secret protection, descriptor storage, and credential resolution adapters.");
}
