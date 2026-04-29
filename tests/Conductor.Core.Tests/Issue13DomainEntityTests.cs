using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.SymphonyReleases;
using Conductor.Core.Domain.Workflows;

namespace Conductor.Core.Tests;

public sealed class Issue13DomainEntityTests
{
    [Fact]
    public void WorkflowProfile_Trims_Name_And_Source_Template()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var profile = new WorkflowProfile(
            WorkflowProfileId.New(),
            "  Default Docker  ",
            "  # WORKFLOW\nrun: symphony  ",
            createdAt);

        Assert.Equal("Default Docker", profile.Name);
        Assert.Equal("# WORKFLOW\nrun: symphony", profile.WorkflowSource);
        Assert.Equal(createdAt, profile.CreatedAtUtc);
    }

    [Fact]
    public void SymphonyReleaseArtifact_Stores_Release_Asset_Provenance()
    {
        var downloadedAt = DateTimeOffset.Parse("2026-04-29T00:05:00Z");
        var artifact = new SymphonyReleaseArtifact(
            "  v1.2.3  ",
            "  symphony-win-x64.zip  ",
            new Uri("https://github.com/ReleasedGroup/Symphony/releases/download/v1.2.3/symphony-win-x64.zip"),
            downloadedAt,
            "  sha256:abc123  ");

        Assert.Equal("v1.2.3", artifact.ReleaseTag.Value);
        Assert.Equal("symphony-win-x64.zip", artifact.AssetName);
        Assert.Equal(downloadedAt, artifact.DownloadedAtUtc);
        Assert.Equal("sha256:abc123", artifact.Checksum);
    }

    [Fact]
    public void SecretDescriptor_Stores_Metadata_Without_A_Secret_Value()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:10:00Z");
        var rotatedAt = createdAt.AddMinutes(30);
        var instanceId = SymphonyInstanceId.New();
        var descriptor = new SecretDescriptor(
            SecretId.New(),
            "  GitHub PAT  ",
            SecretType.GitHubToken,
            SecretScopeType.SymphonyInstance,
            instanceId.ToString(),
            createdAt);

        descriptor.MarkRotated(rotatedAt);

        Assert.Equal("GitHub PAT", descriptor.Name);
        Assert.Equal(SecretType.GitHubToken, descriptor.SecretType);
        Assert.Equal(SecretScopeType.SymphonyInstance, descriptor.ScopeType);
        Assert.Equal(instanceId.ToString(), descriptor.ScopeId);
        Assert.Equal(createdAt, descriptor.CreatedAtUtc);
        Assert.Equal(rotatedAt, descriptor.RotatedAtUtc);
        Assert.DoesNotContain(
            typeof(SecretDescriptor).GetProperties(),
            property => property.Name.Contains("Value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SecretDescriptor_Requires_Scope_Id_For_Non_Global_Secrets()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:10:00Z");

        var error = Assert.Throws<ArgumentException>(() => new SecretDescriptor(
            SecretId.New(),
            "Repository PAT",
            SecretType.GitHubToken,
            SecretScopeType.Repository,
            scopeId: " ",
            createdAt));

        Assert.Equal("scopeId", error.ParamName);
    }

    [Fact]
    public void SecretDescriptor_Rejects_Scope_Id_For_Global_Secrets()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:10:00Z");

        var error = Assert.Throws<ArgumentException>(() => new SecretDescriptor(
            SecretId.New(),
            "Default OpenAI key",
            SecretType.OpenAiApiKey,
            SecretScopeType.Global,
            scopeId: "default",
            createdAt));

        Assert.Equal("scopeId", error.ParamName);
    }
}
