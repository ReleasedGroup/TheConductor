using Conductor.Core.Abstractions.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conductor.Infrastructure.Secrets;

public static class SecretsServiceCollectionExtensions
{
    public static IServiceCollection AddConductorSecrets(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DataProtectionSecretProtector>();
        services.AddScoped<ISecretStore, DataProtectionSecretStore>();

        return services;
    }
}
