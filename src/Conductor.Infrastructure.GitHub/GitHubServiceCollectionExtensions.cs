using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Abstractions.Releases;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Infrastructure.GitHub;

public static class GitHubServiceCollectionExtensions
{
    public static IServiceCollection AddConductorGitHub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IGitHubRepositoryClient, GitHubRepositoryClient>(
            GitHubRepositoryClient.HttpClientName,
            client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddHttpClient<ISymphonyReleaseResolver, GitHubSymphonyReleaseResolver>(
            GitHubSymphonyReleaseResolver.HttpClientName,
            client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });

        return services;
    }
}
