using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Application.Secrets;

public sealed record SecretDescriptorView(
    SecretId Id,
    string Name,
    SecretType SecretType,
    string SecretTypeLabel,
    string EnvironmentVariableName,
    SecretScopeType ScopeType,
    string ScopeLabel,
    string? ScopeId,
    string MaskedValue,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RotatedAtUtc)
{
    public static SecretDescriptorView FromDescriptor(SecretDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        SecretTypeDisplay secretType = SecretTypeMetadata.Get(descriptor.SecretType);

        return new SecretDescriptorView(
            descriptor.Id,
            descriptor.Name,
            descriptor.SecretType,
            secretType.Label,
            secretType.EnvironmentVariableName,
            descriptor.ScopeType,
            FormatScope(descriptor.ScopeType, descriptor.ScopeId),
            descriptor.ScopeId,
            secretType.MaskedDisplayValue,
            descriptor.CreatedAtUtc,
            descriptor.RotatedAtUtc);
    }

    private static string FormatScope(SecretScopeType scopeType, string? scopeId)
    {
        return scopeType switch
        {
            SecretScopeType.Global => "Global",
            SecretScopeType.Project => $"Project {scopeId}",
            SecretScopeType.Repository => $"Repository {scopeId}",
            SecretScopeType.SymphonyInstance => $"Symphony instance {scopeId}",
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "Unsupported secret scope type."),
        };
    }
}
