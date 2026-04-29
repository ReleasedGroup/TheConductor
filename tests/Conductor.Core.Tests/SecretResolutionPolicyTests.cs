using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Tests;

public sealed class SecretResolutionPolicyTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
    private static readonly ProjectId ProjectId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly RepositoryId RepositoryId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly SymphonyInstanceId InstanceId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    [Fact]
    public void Resolve_Inherited_Secret_Prefers_Instance_Scope()
    {
        SecretDescriptor globalSecret = Descriptor(
            new SecretId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            SecretScopeType.Global,
            scopeId: null);
        SecretDescriptor projectSecret = Descriptor(
            new SecretId(Guid.Parse("10000000-0000-0000-0000-000000000002")),
            SecretScopeType.Project,
            ProjectId.ToString());
        SecretDescriptor repositorySecret = Descriptor(
            new SecretId(Guid.Parse("10000000-0000-0000-0000-000000000003")),
            SecretScopeType.Repository,
            RepositoryId.ToString());
        SecretDescriptor instanceSecret = Descriptor(
            new SecretId(Guid.Parse("10000000-0000-0000-0000-000000000004")),
            SecretScopeType.SymphonyInstance,
            InstanceId.ToString());

        SecretDescriptor? resolved = SecretResolutionPolicy.Resolve(
            InheritedRequest(SecretType.GitHubToken),
            [globalSecret, projectSecret, repositorySecret, instanceSecret]);

        Assert.Equal(instanceSecret.Id, resolved?.Id);
    }

    [Fact]
    public void Resolve_Inherited_Secret_Falls_Back_From_Repository_To_Project_Then_Global()
    {
        SecretDescriptor globalSecret = Descriptor(
            new SecretId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            SecretScopeType.Global,
            scopeId: null);
        SecretDescriptor projectSecret = Descriptor(
            new SecretId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            SecretScopeType.Project,
            ProjectId.ToString());
        SecretDescriptor repositorySecret = Descriptor(
            new SecretId(Guid.Parse("20000000-0000-0000-0000-000000000003")),
            SecretScopeType.Repository,
            RepositoryId.ToString());

        Assert.Equal(
            repositorySecret.Id,
            SecretResolutionPolicy.Resolve(
                InheritedRequest(SecretType.GitHubToken),
                [globalSecret, projectSecret, repositorySecret])?.Id);
        Assert.Equal(
            projectSecret.Id,
            SecretResolutionPolicy.Resolve(
                InheritedRequest(SecretType.GitHubToken),
                [globalSecret, projectSecret])?.Id);
        Assert.Equal(
            globalSecret.Id,
            SecretResolutionPolicy.Resolve(
                InheritedRequest(SecretType.GitHubToken),
                [globalSecret])?.Id);
    }

    [Fact]
    public void Resolve_Inherited_Secret_Does_Not_Cross_Secret_Types()
    {
        SecretDescriptor openAiSecret = Descriptor(
            new SecretId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
            SecretScopeType.SymphonyInstance,
            InstanceId.ToString(),
            SecretType.OpenAiApiKey);
        SecretDescriptor gitHubSecret = Descriptor(
            new SecretId(Guid.Parse("30000000-0000-0000-0000-000000000002")),
            SecretScopeType.Global,
            scopeId: null,
            SecretType.GitHubToken);

        SecretDescriptor? resolved = SecretResolutionPolicy.Resolve(
            InheritedRequest(SecretType.GitHubToken),
            [openAiSecret, gitHubSecret]);

        Assert.Equal(gitHubSecret.Id, resolved?.Id);
    }

    [Fact]
    public void Resolve_Returns_Null_When_Credential_Mode_Is_None()
    {
        SecretDescriptor instanceSecret = Descriptor(
            new SecretId(Guid.Parse("40000000-0000-0000-0000-000000000001")),
            SecretScopeType.SymphonyInstance,
            InstanceId.ToString());
        SecretResolutionRequest request = new(
            SecretType.GitHubToken,
            InstanceId,
            RepositoryId,
            ProjectId,
            CredentialInheritanceMode.None);

        SecretDescriptor? resolved = SecretResolutionPolicy.Resolve(request, [instanceSecret]);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_Specific_Secret_Uses_Selected_Descriptor()
    {
        SecretDescriptor instanceSecret = Descriptor(
            new SecretId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
            SecretScopeType.SymphonyInstance,
            InstanceId.ToString());
        SecretDescriptor selectedRepositorySecret = Descriptor(
            new SecretId(Guid.Parse("50000000-0000-0000-0000-000000000002")),
            SecretScopeType.Repository,
            RepositoryId.ToString());
        SecretResolutionRequest request = new(
            SecretType.GitHubToken,
            InstanceId,
            RepositoryId,
            ProjectId,
            CredentialInheritanceMode.SpecificSecret,
            selectedRepositorySecret.Id);

        SecretDescriptor? resolved = SecretResolutionPolicy.Resolve(
            request,
            [instanceSecret, selectedRepositorySecret]);

        Assert.Equal(selectedRepositorySecret.Id, resolved?.Id);
    }

    [Fact]
    public void Resolve_Inherited_Secret_Uses_Most_Recently_Rotated_Match_Within_A_Scope()
    {
        SecretDescriptor olderSecret = Descriptor(
            new SecretId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            SecretScopeType.Repository,
            RepositoryId.ToString());
        SecretDescriptor rotatedSecret = Descriptor(
            new SecretId(Guid.Parse("60000000-0000-0000-0000-000000000002")),
            SecretScopeType.Repository,
            RepositoryId.ToString());

        rotatedSecret.MarkRotated(CreatedAtUtc.AddMinutes(10));

        SecretDescriptor? resolved = SecretResolutionPolicy.Resolve(
            InheritedRequest(SecretType.GitHubToken),
            [olderSecret, rotatedSecret]);

        Assert.Equal(rotatedSecret.Id, resolved?.Id);
    }

    [Fact]
    public void GetInheritanceCandidates_Uses_Documented_Precedence_And_Omits_Missing_Project()
    {
        SecretResolutionRequest request = new(
            SecretType.OpenAiApiKey,
            InstanceId,
            RepositoryId,
            projectId: null,
            CredentialInheritanceMode.InheritDefault);

        IReadOnlyList<SecretResolutionCandidate> candidates = SecretResolutionPolicy.GetInheritanceCandidates(request);

        Assert.Collection(
            candidates,
            candidate =>
            {
                Assert.Equal(SecretScopeType.SymphonyInstance, candidate.ScopeType);
                Assert.Equal(InstanceId.ToString(), candidate.ScopeId);
            },
            candidate =>
            {
                Assert.Equal(SecretScopeType.Repository, candidate.ScopeType);
                Assert.Equal(RepositoryId.ToString(), candidate.ScopeId);
            },
            candidate =>
            {
                Assert.Equal(SecretScopeType.Global, candidate.ScopeType);
                Assert.Null(candidate.ScopeId);
            });
    }

    [Fact]
    public void SecretResolutionRequest_Requires_Specific_Secret_Id_Only_For_Specific_Mode()
    {
        var missingSecretError = Assert.Throws<ArgumentException>(() => new SecretResolutionRequest(
            SecretType.GitHubToken,
            InstanceId,
            RepositoryId,
            ProjectId,
            CredentialInheritanceMode.SpecificSecret));
        var inheritedSecretError = Assert.Throws<ArgumentException>(() => new SecretResolutionRequest(
            SecretType.GitHubToken,
            InstanceId,
            RepositoryId,
            ProjectId,
            CredentialInheritanceMode.InheritDefault,
            SecretId.New()));

        Assert.Equal("secretId", missingSecretError.ParamName);
        Assert.Equal("secretId", inheritedSecretError.ParamName);
    }

    private static SecretResolutionRequest InheritedRequest(SecretType secretType) =>
        new(
            secretType,
            InstanceId,
            RepositoryId,
            ProjectId,
            CredentialInheritanceMode.InheritDefault);

    private static SecretDescriptor Descriptor(
        SecretId id,
        SecretScopeType scopeType,
        string? scopeId,
        SecretType secretType = SecretType.GitHubToken)
    {
        return new SecretDescriptor(
            id,
            $"{scopeType} {secretType}",
            secretType,
            scopeType,
            scopeId,
            CreatedAtUtc);
    }
}
