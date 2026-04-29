using Conductor.Core.Common;

namespace Conductor.Core.Domain.SymphonyReleases;

public sealed class SymphonyReleaseArtifact
{
    public SymphonyReleaseArtifact(
        string releaseTag,
        string assetName,
        Uri sourceUrl,
        DateTimeOffset downloadedAtUtc,
        string? checksum)
    {
        ReleaseTag = Guard.NotWhiteSpace(releaseTag, nameof(releaseTag));
        AssetName = Guard.NotWhiteSpace(assetName, nameof(assetName));
        SourceUrl = Guard.AbsoluteUri(sourceUrl, nameof(sourceUrl));
        DownloadedAtUtc = downloadedAtUtc;
        Checksum = string.IsNullOrWhiteSpace(checksum) ? null : checksum.Trim();
    }

    public string ReleaseTag { get; }

    public string AssetName { get; }

    public Uri SourceUrl { get; }

    public DateTimeOffset DownloadedAtUtc { get; }

    public string? Checksum { get; }
}
