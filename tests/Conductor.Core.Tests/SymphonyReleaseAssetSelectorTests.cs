using Conductor.Core.Domain;
using Conductor.Core.Domain.SymphonyReleases;

namespace Conductor.Core.Tests;

public sealed class SymphonyReleaseAssetSelectorTests
{
    [Fact]
    public void SelectCompatibleAsset_Selects_Windows_X64_Zip_For_Local_Process()
    {
        SymphonyReleaseAssetSelectionResult result = SymphonyReleaseAssetSelector.SelectCompatibleAsset(
            CreateSymphonyReleaseAssets(),
            new SymphonyReleaseAssetSelectionTarget(
                ExecutionMode.LocalProcess,
                ReleaseAssetOperatingSystem.Windows,
                ReleaseAssetArchitecture.X64));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Selection);
        Assert.Equal("Symphony-0.0.7-alpha-win-x64.zip", result.Selection.Asset.Name);
        Assert.Equal("Symphony-0.0.7-alpha-win-x64.zip.sha256", result.Selection.ChecksumAsset?.Name);
        Assert.Equal("sha256:win-x64-digest", result.Selection.Asset.Digest);
    }

    [Fact]
    public void SelectCompatibleAsset_Selects_MacOS_Arm64_Tarball_For_Local_Process()
    {
        SymphonyReleaseAssetSelectionResult result = SymphonyReleaseAssetSelector.SelectCompatibleAsset(
            CreateSymphonyReleaseAssets(),
            new SymphonyReleaseAssetSelectionTarget(
                ExecutionMode.LocalProcess,
                ReleaseAssetOperatingSystem.MacOS,
                ReleaseAssetArchitecture.Arm64));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Selection);
        Assert.Equal("Symphony-0.0.7-alpha-osx-arm64.tar.gz", result.Selection.Asset.Name);
    }

    [Fact]
    public void SelectCompatibleAsset_Prefers_Container_Metadata_For_Docker()
    {
        SymphonyReleaseAssetSelectionResult result = SymphonyReleaseAssetSelector.SelectCompatibleAsset(
            [
                Asset("Symphony-0.0.7-alpha-linux-x64.tar.gz"),
                Asset("Symphony-0.0.7-alpha-linux-x64-docker-image.json"),
            ],
            new SymphonyReleaseAssetSelectionTarget(
                ExecutionMode.Docker,
                ReleaseAssetOperatingSystem.Linux,
                ReleaseAssetArchitecture.X64));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Selection);
        Assert.Equal("Symphony-0.0.7-alpha-linux-x64-docker-image.json", result.Selection.Asset.Name);
    }

    [Fact]
    public void SelectCompatibleAsset_Falls_Back_To_Linux_Archive_For_Docker()
    {
        SymphonyReleaseAssetSelectionResult result = SymphonyReleaseAssetSelector.SelectCompatibleAsset(
            CreateSymphonyReleaseAssets(),
            new SymphonyReleaseAssetSelectionTarget(
                ExecutionMode.Docker,
                ReleaseAssetOperatingSystem.Linux,
                ReleaseAssetArchitecture.Arm64));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Selection);
        Assert.Equal("Symphony-0.0.7-alpha-linux-arm64.tar.gz", result.Selection.Asset.Name);
    }

    [Fact]
    public void SelectCompatibleAsset_Does_Not_Select_Checksum_As_Primary_Asset()
    {
        SymphonyReleaseAssetSelectionResult result = SymphonyReleaseAssetSelector.SelectCompatibleAsset(
            [
                Asset("Symphony-0.0.7-alpha-linux-x64.tar.gz.sha256"),
                Asset("Symphony-0.0.7-alpha-linux-x64.tar.gz"),
            ],
            new SymphonyReleaseAssetSelectionTarget(
                ExecutionMode.LocalProcess,
                ReleaseAssetOperatingSystem.Linux,
                ReleaseAssetArchitecture.X64));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Selection);
        Assert.Equal("Symphony-0.0.7-alpha-linux-x64.tar.gz", result.Selection.Asset.Name);
    }

    [Fact]
    public void SelectCompatibleAsset_Returns_Failure_When_No_Target_Matches()
    {
        SymphonyReleaseAssetSelectionResult result = SymphonyReleaseAssetSelector.SelectCompatibleAsset(
            [Asset("Symphony-0.0.7-alpha-win-x64.zip")],
            new SymphonyReleaseAssetSelectionTarget(
                ExecutionMode.LocalProcess,
                ReleaseAssetOperatingSystem.Linux,
                ReleaseAssetArchitecture.Arm64));

        Assert.False(result.Succeeded);
        Assert.Null(result.Selection);
        Assert.Contains("LocalProcess/Linux/Arm64", result.FailureReason, StringComparison.Ordinal);
    }

    private static IReadOnlyList<SymphonyReleaseAsset> CreateSymphonyReleaseAssets() =>
    [
        Asset("Symphony-0.0.7-alpha-linux-arm64.tar.gz"),
        Asset("Symphony-0.0.7-alpha-linux-arm64.tar.gz.sha256"),
        Asset("Symphony-0.0.7-alpha-linux-x64.tar.gz"),
        Asset("Symphony-0.0.7-alpha-linux-x64.tar.gz.sha256"),
        Asset("Symphony-0.0.7-alpha-osx-arm64.tar.gz"),
        Asset("Symphony-0.0.7-alpha-osx-arm64.tar.gz.sha256"),
        Asset("Symphony-0.0.7-alpha-osx-x64.tar.gz"),
        Asset("Symphony-0.0.7-alpha-osx-x64.tar.gz.sha256"),
        Asset("Symphony-0.0.7-alpha-win-arm64.zip"),
        Asset("Symphony-0.0.7-alpha-win-arm64.zip.sha256"),
        Asset("Symphony-0.0.7-alpha-win-x64.zip", digest: "sha256:win-x64-digest"),
        Asset("Symphony-0.0.7-alpha-win-x64.zip.sha256"),
    ];

    private static SymphonyReleaseAsset Asset(string name, string? digest = null) =>
        new(
            name,
            new Uri($"https://github.com/ReleasedGroup/Symphony/releases/download/v0.0.7-alpha/{name}"),
            sizeBytes: 1024,
            contentType: null,
            digest);
}
