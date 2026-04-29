using Conductor.Core.Application;

namespace Conductor.Infrastructure.GitHub;

public static class GitHubInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.GitHub",
        "GitHub repository discovery, metadata, and Symphony release resolution adapters.");
}
