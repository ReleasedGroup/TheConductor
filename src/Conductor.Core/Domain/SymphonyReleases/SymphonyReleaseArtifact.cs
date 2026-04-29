using Conductor.Core.Common;
using Conductor.Core.Domain.Releases;

namespace Conductor.Core.Domain.SymphonyReleases;

public sealed class SymphonyReleaseArtifact
{
    public SymphonyReleaseArtifact(
        ReleaseTag releaseTag,
        string assetName,
        Uri sourceUrl,
        DateTimeOffset downloadedAtUtc,
        string? checksum)
    {
        ReleaseTag = releaseTag ?? throw new ArgumentNullException(nameof(releaseTag));
        AssetName = Guard.NotWhiteSpace(assetName, nameof(assetName));
        SourceUrl = Guard.AbsoluteUri(sourceUrl, nameof(sourceUrl));
        DownloadedAtUtc = downloadedAtUtc;
        Checksum = string.IsNullOrWhiteSpace(checksum) ? null : checksum.Trim();
    }

    public SymphonyReleaseArtifact(
        string releaseTag,
        string assetName,
        Uri sourceUrl,
        DateTimeOffset downloadedAtUtc,
        string? checksum)
        : this(new ReleaseTag(releaseTag), assetName, sourceUrl, downloadedAtUtc, checksum)
    {
    }

    public ReleaseTag ReleaseTag { get; }

    public string AssetName { get; }

    public Uri SourceUrl { get; }

    public DateTimeOffset DownloadedAtUtc { get; }

    public string? Checksum { get; }
}
