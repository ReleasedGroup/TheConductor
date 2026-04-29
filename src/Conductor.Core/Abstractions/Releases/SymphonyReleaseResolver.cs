using System.Runtime.InteropServices;
using Conductor.Core.Common;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Releases;
using Conductor.Core.Domain.SymphonyReleases;

namespace Conductor.Core.Abstractions.Releases;

public interface ISymphonyReleaseResolver
{
    Task<ResolvedSymphonyRelease> ResolveAsync(
        ReleaseSelector selector,
        RuntimeTarget target,
        CancellationToken cancellationToken);
}

public sealed record RuntimeTarget
{
    public RuntimeTarget(
        ExecutionMode executionMode,
        string operatingSystem,
        string architecture,
        RuntimeArtifactPreference artifactPreference = RuntimeArtifactPreference.Auto)
    {
        if (!Enum.IsDefined(executionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionMode),
                executionMode,
                "Execution mode is not supported.");
        }

        if (!Enum.IsDefined(artifactPreference))
        {
            throw new ArgumentOutOfRangeException(
                nameof(artifactPreference),
                artifactPreference,
                "Runtime artifact preference is not supported.");
        }

        ExecutionMode = executionMode;
        OperatingSystem = NormalizeToken(operatingSystem, nameof(operatingSystem));
        Architecture = NormalizeToken(architecture, nameof(architecture));
        ArtifactPreference = artifactPreference;
    }

    public ExecutionMode ExecutionMode { get; }

    public string OperatingSystem { get; }

    public string Architecture { get; }

    public RuntimeArtifactPreference ArtifactPreference { get; }

    public static RuntimeTarget Current(
        ExecutionMode executionMode,
        RuntimeArtifactPreference artifactPreference = RuntimeArtifactPreference.Auto) =>
        new(
            executionMode,
            ResolveCurrentOperatingSystem(),
            ResolveCurrentArchitecture(),
            artifactPreference);

    public override string ToString() =>
        $"{ExecutionMode} {OperatingSystem}/{Architecture} ({ArtifactPreference})";

    private static string ResolveCurrentOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macos";
        }

        return "unknown";
    }

    private static string ResolveCurrentArchitecture() =>
        RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.Arm => "arm",
            System.Runtime.InteropServices.Architecture.S390x => "s390x",
            System.Runtime.InteropServices.Architecture.Wasm => "wasm",
            System.Runtime.InteropServices.Architecture.LoongArch64 => "loongarch64",
            System.Runtime.InteropServices.Architecture.Armv6 => "armv6",
            System.Runtime.InteropServices.Architecture.Ppc64le => "ppc64le",
            _ => "unknown",
        };

    private static string NormalizeToken(string value, string parameterName) =>
        Guard.NotWhiteSpace(value, parameterName).ToLowerInvariant();
}

public enum RuntimeArtifactPreference
{
    Auto,
    ReleaseArchive,
    ContainerImage,
}

public enum SymphonyReleaseAssetKind
{
    ReleaseArchive,
    ContainerImageMetadata,
    SourceArchive,
}

public sealed record ResolvedSymphonyReleaseAsset
{
    public ResolvedSymphonyReleaseAsset(
        string name,
        Uri sourceUrl,
        long sizeBytes,
        string? contentType,
        string? checksum,
        SymphonyReleaseAssetKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Symphony release asset kind is not supported.");
        }

        Name = Guard.NotWhiteSpace(name, nameof(name));
        SourceUrl = Guard.AbsoluteUri(sourceUrl, nameof(sourceUrl));
        SizeBytes = Guard.NonNegative(sizeBytes, nameof(sizeBytes));
        ContentType = Guard.OptionalTrimmed(contentType);
        Checksum = Guard.OptionalTrimmed(checksum);
        Kind = kind;
    }

    public string Name { get; }

    public Uri SourceUrl { get; }

    public long SizeBytes { get; }

    public string? ContentType { get; }

    public string? Checksum { get; }

    public SymphonyReleaseAssetKind Kind { get; }
}

public sealed record ResolvedSymphonyRelease
{
    public ResolvedSymphonyRelease(
        ReleaseSelector requestedSelector,
        ReleaseTag releaseTag,
        Uri releaseUrl,
        DateTimeOffset? publishedAtUtc,
        bool isPrerelease,
        ResolvedSymphonyReleaseAsset selectedAsset,
        IReadOnlyList<ResolvedSymphonyReleaseAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(requestedSelector);
        ArgumentNullException.ThrowIfNull(releaseTag);
        ArgumentNullException.ThrowIfNull(selectedAsset);
        ArgumentNullException.ThrowIfNull(assets);

        ResolvedSymphonyReleaseAsset[] copiedAssets = assets.ToArray();

        if (copiedAssets.Length == 0)
        {
            throw new ArgumentException("At least one release asset is required.", nameof(assets));
        }

        if (!copiedAssets.Contains(selectedAsset))
        {
            throw new ArgumentException("The selected asset must be included in the asset list.", nameof(selectedAsset));
        }

        RequestedSelector = requestedSelector;
        ReleaseTag = releaseTag;
        ReleaseUrl = Guard.AbsoluteUri(releaseUrl, nameof(releaseUrl));
        PublishedAtUtc = publishedAtUtc is null
            ? null
            : Guard.Utc(publishedAtUtc.Value, nameof(publishedAtUtc));
        IsPrerelease = isPrerelease;
        SelectedAsset = selectedAsset;
        Assets = copiedAssets;
    }

    public ReleaseSelector RequestedSelector { get; }

    public ReleaseTag ReleaseTag { get; }

    public Uri ReleaseUrl { get; }

    public DateTimeOffset? PublishedAtUtc { get; }

    public bool IsPrerelease { get; }

    public ResolvedSymphonyReleaseAsset SelectedAsset { get; }

    public IReadOnlyList<ResolvedSymphonyReleaseAsset> Assets { get; }

    public SymphonyReleaseArtifact ToArtifact(DateTimeOffset downloadedAtUtc) =>
        new(
            ReleaseTag,
            SelectedAsset.Name,
            SelectedAsset.SourceUrl,
            Guard.Utc(downloadedAtUtc, nameof(downloadedAtUtc)),
            SelectedAsset.Checksum);
}

public sealed class SymphonyReleaseResolutionException : Exception
{
    public SymphonyReleaseResolutionException(string message)
        : base(message)
    {
    }

    public SymphonyReleaseResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
