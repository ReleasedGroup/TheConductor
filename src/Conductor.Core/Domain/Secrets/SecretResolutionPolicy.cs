using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Secrets;

public sealed class SecretResolutionRequest
{
    public SecretResolutionRequest(
        SecretType secretType,
        SymphonyInstanceId symphonyInstanceId,
        RepositoryId repositoryId,
        ProjectId? projectId,
        CredentialInheritanceMode inheritanceMode,
        SecretId? specificSecretId = null)
    {
        if (!Enum.IsDefined(inheritanceMode))
        {
            throw new ArgumentOutOfRangeException(nameof(inheritanceMode), inheritanceMode, "Credential inheritance mode is not supported.");
        }

        SecretType = secretType;
        SymphonyInstanceId = new SymphonyInstanceId(Guard.NotEmpty(symphonyInstanceId.Value, nameof(symphonyInstanceId)));
        RepositoryId = new RepositoryId(Guard.NotEmpty(repositoryId.Value, nameof(repositoryId)));
        ProjectId = projectId is null
            ? null
            : new ProjectId(Guard.NotEmpty(projectId.Value.Value, nameof(projectId)));
        InheritanceMode = inheritanceMode;
        SpecificSecretId = ValidateSpecificSecretId(specificSecretId, inheritanceMode);
    }

    public SecretType SecretType { get; }

    public SymphonyInstanceId SymphonyInstanceId { get; }

    public RepositoryId RepositoryId { get; }

    public ProjectId? ProjectId { get; }

    public CredentialInheritanceMode InheritanceMode { get; }

    public SecretId? SpecificSecretId { get; }

    private static SecretId? ValidateSpecificSecretId(
        SecretId? secretId,
        CredentialInheritanceMode inheritanceMode)
    {
        if (inheritanceMode == CredentialInheritanceMode.SpecificSecret)
        {
            if (secretId is null)
            {
                throw new ArgumentException("A specific secret resolution request requires a secret id.", nameof(secretId));
            }

            return new SecretId(Guard.NotEmpty(secretId.Value.Value, nameof(secretId)));
        }

        if (secretId is not null)
        {
            throw new ArgumentException("A secret id can only be supplied for specific secret resolution.", nameof(secretId));
        }

        return null;
    }
}

public sealed record SecretResolutionCandidate(SecretScopeType ScopeType, string? ScopeId);

public static class SecretResolutionPolicy
{
    public static SecretDescriptor? Resolve(
        SecretResolutionRequest request,
        IEnumerable<SecretDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(descriptors);

        SecretDescriptor[] matchingTypeDescriptors = descriptors
            .Where(descriptor => descriptor.SecretType == request.SecretType)
            .ToArray();

        return request.InheritanceMode switch
        {
            CredentialInheritanceMode.None => null,
            CredentialInheritanceMode.SpecificSecret => ResolveSpecificSecret(request, matchingTypeDescriptors),
            CredentialInheritanceMode.InheritDefault => ResolveInheritedSecret(request, matchingTypeDescriptors),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.InheritanceMode,
                "Credential inheritance mode is not supported."),
        };
    }

    public static IReadOnlyList<SecretResolutionCandidate> GetInheritanceCandidates(
        SecretResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InheritanceMode != CredentialInheritanceMode.InheritDefault)
        {
            return [];
        }

        List<SecretResolutionCandidate> candidates =
        [
            new(SecretScopeType.SymphonyInstance, request.SymphonyInstanceId.ToString()),
            new(SecretScopeType.Repository, request.RepositoryId.ToString()),
        ];

        if (request.ProjectId is { } projectId)
        {
            candidates.Add(new SecretResolutionCandidate(SecretScopeType.Project, projectId.ToString()));
        }

        candidates.Add(new SecretResolutionCandidate(SecretScopeType.Global, null));

        return candidates;
    }

    private static SecretDescriptor? ResolveSpecificSecret(
        SecretResolutionRequest request,
        IEnumerable<SecretDescriptor> descriptors)
    {
        SecretId specificSecretId = request.SpecificSecretId!.Value;

        return descriptors.FirstOrDefault(descriptor => descriptor.Id == specificSecretId);
    }

    private static SecretDescriptor? ResolveInheritedSecret(
        SecretResolutionRequest request,
        IReadOnlyList<SecretDescriptor> descriptors)
    {
        foreach (SecretResolutionCandidate candidate in GetInheritanceCandidates(request))
        {
            SecretDescriptor? resolved = descriptors
                .Where(descriptor => MatchesCandidate(descriptor, candidate))
                .OrderByDescending(descriptor => descriptor.RotatedAtUtc ?? descriptor.CreatedAtUtc)
                .ThenByDescending(descriptor => descriptor.CreatedAtUtc)
                .ThenByDescending(descriptor => descriptor.Id.Value)
                .FirstOrDefault();

            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static bool MatchesCandidate(
        SecretDescriptor descriptor,
        SecretResolutionCandidate candidate)
    {
        return descriptor.ScopeType == candidate.ScopeType &&
            string.Equals(descriptor.ScopeId, candidate.ScopeId, StringComparison.OrdinalIgnoreCase);
    }
}
