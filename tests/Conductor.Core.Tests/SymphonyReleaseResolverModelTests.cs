using Conductor.Core.Abstractions.Releases;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Releases;

namespace Conductor.Core.Tests;

public sealed class SymphonyReleaseResolverModelTests
{
    [Fact]
    public void RuntimeTarget_Normalizes_Platform_Tokens()
    {
        var target = new RuntimeTarget(
            ExecutionMode.LocalProcess,
            " Windows ",
            " X64 ",
            RuntimeArtifactPreference.ReleaseArchive);

        Assert.Equal(ExecutionMode.LocalProcess, target.ExecutionMode);
        Assert.Equal("windows", target.OperatingSystem);
        Assert.Equal("x64", target.Architecture);
        Assert.Equal(RuntimeArtifactPreference.ReleaseArchive, target.ArtifactPreference);
        Assert.Equal("LocalProcess windows/x64 (ReleaseArchive)", target.ToString());
    }

    [Fact]
    public void ResolvedSymphonyRelease_Creates_Persistable_Artifact_Provenance()
    {
        var selectedAsset = new ResolvedSymphonyReleaseAsset(
            "symphony-linux-x64.tar.gz",
            new Uri("https://github.com/releasedgroup/symphony/releases/download/v0.0.7-alpha/symphony-linux-x64.tar.gz"),
            42_000,
            "application/gzip",
            "sha256:abc123",
            SymphonyReleaseAssetKind.ReleaseArchive);
        var release = new ResolvedSymphonyRelease(
            ReleaseSelector.Latest,
            new ReleaseTag("v0.0.7-alpha"),
            new Uri("https://github.com/releasedgroup/symphony/releases/tag/v0.0.7-alpha"),
            DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
            isPrerelease: true,
            selectedAsset,
            [selectedAsset]);

        var artifact = release.ToArtifact(DateTimeOffset.Parse("2026-04-29T01:00:00Z"));

        Assert.Equal("v0.0.7-alpha", artifact.ReleaseTag.Value);
        Assert.Equal("symphony-linux-x64.tar.gz", artifact.AssetName);
        Assert.Equal(selectedAsset.SourceUrl, artifact.SourceUrl);
        Assert.Equal("sha256:abc123", artifact.Checksum);
    }
}
