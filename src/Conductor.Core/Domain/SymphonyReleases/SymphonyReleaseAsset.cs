using Conductor.Core.Common;

namespace Conductor.Core.Domain.SymphonyReleases;

public sealed record SymphonyReleaseAsset
{
    public SymphonyReleaseAsset(
        string name,
        Uri downloadUrl,
        long sizeBytes,
        string? contentType = null,
        string? digest = null)
    {
        Name = Guard.NotWhiteSpace(name, nameof(name));
        DownloadUrl = Guard.AbsoluteUri(downloadUrl, nameof(downloadUrl));
        SizeBytes = Guard.NonNegative(sizeBytes, nameof(sizeBytes));
        ContentType = Guard.OptionalTrimmed(contentType);
        Digest = Guard.OptionalTrimmed(digest);
    }

    public string Name { get; }

    public Uri DownloadUrl { get; }

    public long SizeBytes { get; }

    public string? ContentType { get; }

    public string? Digest { get; }
}
