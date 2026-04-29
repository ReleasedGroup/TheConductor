using Conductor.Core.Abstractions.Symphony;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Infrastructure.Symphony;

public static class SymphonyServiceCollectionExtensions
{
    public static IServiceCollection AddConductorSymphony(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<ISymphonyApiClient, SymphonyApiClient>(
            SymphonyApiClient.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(15));

        return services;
    }
}
