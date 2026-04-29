using Conductor.Core.Abstractions.Symphony;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Infrastructure.Symphony;

public static class SymphonyServiceCollectionExtensions
{
    public static IServiceCollection AddConductorSymphony(this IServiceCollection services)
    {
        services
            .AddHttpClient<ISymphonyApiClient, SymphonyApiClient>(SymphonyApiClient.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }
}
