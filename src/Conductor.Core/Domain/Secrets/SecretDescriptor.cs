using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Secrets;

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

public sealed class SecretDescriptor
{
    public SecretDescriptor(
        SecretId id,
        string name,
        SecretType secretType,
        SecretScopeType scopeType,
        string? scopeId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? rotatedAtUtc = null)
    {
        Id = id;
        Name = Guard.NotWhiteSpace(name, nameof(name));
        SecretType = secretType;
        ScopeType = scopeType;
        ScopeId = NormalizeScopeId(scopeType, scopeId);
        CreatedAtUtc = createdAtUtc;
        RotatedAtUtc = rotatedAtUtc;
    }

    public SecretId Id { get; }

    public string Name { get; }

    public SecretType SecretType { get; }

    public SecretScopeType ScopeType { get; }

    public string? ScopeId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? RotatedAtUtc { get; private set; }

    public void MarkRotated(DateTimeOffset rotatedAtUtc)
    {
        RotatedAtUtc = rotatedAtUtc;
    }

    private static string? NormalizeScopeId(SecretScopeType scopeType, string? scopeId)
    {
        if (scopeType is SecretScopeType.Global)
        {
            if (!string.IsNullOrWhiteSpace(scopeId))
            {
                throw new ArgumentException("Global secrets cannot have a scope identifier.", nameof(scopeId));
            }

            return null;
        }

        return Guard.NotWhiteSpace(scopeId ?? string.Empty, nameof(scopeId));
    }
}
