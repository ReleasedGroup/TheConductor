using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

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

public sealed record CreateSecretRequest(
    string Name,
    SecretType SecretType,
    SecretScopeType ScopeType,
    string? ScopeId,
    string Value)
{
    public override string ToString() =>
        $"{nameof(CreateSecretRequest)} {{ {nameof(Name)} = {Name}, {nameof(SecretType)} = {SecretType}, {nameof(ScopeType)} = {ScopeType}, {nameof(ScopeId)} = {ScopeId}, {nameof(Value)} = ******** }}";
}

public sealed record RotateSecretRequest(string Value)
{
    public override string ToString() => $"{nameof(RotateSecretRequest)} {{ {nameof(Value)} = ******** }}";
}

public sealed record SecretReference(
    SecretId SecretId,
    CredentialInheritanceMode InheritanceMode);

public sealed record ResolvedSecret(SecretId SecretId, string Value)
{
    public override string ToString() => $"{nameof(ResolvedSecret)} {{ {nameof(SecretId)} = {SecretId}, {nameof(Value)} = ******** }}";
}

public sealed record SecretQuery(SecretType? SecretType = null, SecretScopeType? ScopeType = null);
