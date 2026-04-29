using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Application.Queries;

public interface ISecretDescriptorQueryService
{
    Task<IReadOnlyList<SecretDescriptorProjection>> ListSecretDescriptorsAsync(
        SecretDescriptorQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record SecretDescriptorQuery(
    SecretType? SecretType = null,
    SecretScopeType? ScopeType = null);

public sealed record SecretDescriptorProjection(
    SecretId Id,
    string Name,
    SecretType SecretType,
    SecretScopeType ScopeType,
    string? ScopeId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RotatedAtUtc);
