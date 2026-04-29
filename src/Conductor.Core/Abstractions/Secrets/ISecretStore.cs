using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Abstractions.Secrets;

public interface ISecretStore
{
    Task<SecretDescriptor> CreateAsync(CreateSecretRequest request, CancellationToken cancellationToken);

    Task RotateAsync(
        SecretId secretId,
        RotateSecretRequest request,
        CancellationToken cancellationToken);

    Task<ResolvedSecret> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SecretDescriptor>> ListAsync(
        SecretQuery query,
        CancellationToken cancellationToken);

    Task DeleteAsync(SecretId secretId, CancellationToken cancellationToken);
}

public enum SecretScopeType
{
    Global,
    Project,
    Repository,
    SymphonyInstance,
}

public enum SecretType
{
    GitHubToken,
    OpenAiApiKey,
    CodexHome,
    Other,
}

public sealed record CreateSecretRequest(
    string Name,
    SecretType SecretType,
    SecretScopeType ScopeType,
    string? ScopeId,
    string Value);

public sealed record RotateSecretRequest(string Value);

public sealed record SecretReference(
    SecretId SecretId,
    CredentialInheritanceMode InheritanceMode);

public sealed record ResolvedSecret(SecretId SecretId, string Value);

public sealed record SecretQuery(SecretType? SecretType = null, SecretScopeType? ScopeType = null);

public sealed record SecretDescriptor(
    SecretId Id,
    string Name,
    SecretType SecretType,
    SecretScopeType ScopeType,
    string? ScopeId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RotatedAtUtc);
