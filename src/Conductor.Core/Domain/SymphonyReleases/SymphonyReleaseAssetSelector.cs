using Conductor.Core.Domain;

namespace Conductor.Core.Domain.SymphonyReleases;

public enum ReleaseAssetOperatingSystem
{
    Windows,
    Linux,
    MacOS,
}

public enum ReleaseAssetArchitecture
{
    X64,
    Arm64,
}

public sealed record SymphonyReleaseAssetSelectionTarget
{
    public SymphonyReleaseAssetSelectionTarget(
        ExecutionMode executionMode,
        ReleaseAssetOperatingSystem operatingSystem,
        ReleaseAssetArchitecture architecture)
    {
        ValidateEnum(executionMode, nameof(executionMode));
        ValidateEnum(operatingSystem, nameof(operatingSystem));
        ValidateEnum(architecture, nameof(architecture));

        ExecutionMode = executionMode;
        OperatingSystem = operatingSystem;
        Architecture = architecture;
    }

    public ExecutionMode ExecutionMode { get; }

    public ReleaseAssetOperatingSystem OperatingSystem { get; }

    public ReleaseAssetArchitecture Architecture { get; }

    public override string ToString() => $"{ExecutionMode}/{OperatingSystem}/{Architecture}";

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unsupported release asset target value.");
        }
    }
}

public sealed record SymphonyReleaseAssetSelection(
    SymphonyReleaseAsset Asset,
    SymphonyReleaseAsset? ChecksumAsset,
    SymphonyReleaseAssetSelectionTarget Target);

public sealed record SymphonyReleaseAssetSelectionResult
{
    private SymphonyReleaseAssetSelectionResult(
        SymphonyReleaseAssetSelection? selection,
        string? failureReason)
    {
        Selection = selection;
        FailureReason = failureReason;
    }

    public bool Succeeded => Selection is not null;

    public SymphonyReleaseAssetSelection? Selection { get; }

    public string? FailureReason { get; }

    public static SymphonyReleaseAssetSelectionResult Success(SymphonyReleaseAssetSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return new SymphonyReleaseAssetSelectionResult(selection, failureReason: null);
    }

    public static SymphonyReleaseAssetSelectionResult Failure(string failureReason) =>
        new(selection: null, failureReason);
}

public static class SymphonyReleaseAssetSelector
{
    public static SymphonyReleaseAssetSelectionResult SelectCompatibleAsset(
        IEnumerable<SymphonyReleaseAsset> assets,
        SymphonyReleaseAssetSelectionTarget target)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(target);

        List<SymphonyReleaseAsset> allAssets = assets.ToList();

        if (allAssets.Count == 0)
        {
            return SymphonyReleaseAssetSelectionResult.Failure(
                $"No Symphony release assets were available for {target}.");
        }

        SymphonyReleaseAsset? selectedAsset = allAssets
            .Where(asset => !IsChecksumAsset(asset.Name))
            .Select(asset => new ScoredAsset(asset, ScoreAsset(asset.Name, target)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Asset.Name, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Asset)
            .FirstOrDefault();

        if (selectedAsset is null)
        {
            return SymphonyReleaseAssetSelectionResult.Failure(
                $"No compatible Symphony release asset was found for {target}.");
        }

        SymphonyReleaseAsset? checksumAsset = FindChecksumAsset(allAssets, selectedAsset);
        return SymphonyReleaseAssetSelectionResult.Success(
            new SymphonyReleaseAssetSelection(selectedAsset, checksumAsset, target));
    }

    private static int ScoreAsset(string assetName, SymphonyReleaseAssetSelectionTarget target)
    {
        AssetNameTokens tokens = AssetNameTokens.Parse(assetName);

        if (!tokens.Matches(target.OperatingSystem) || !tokens.Matches(target.Architecture))
        {
            return 0;
        }

        return target.ExecutionMode switch
        {
            ExecutionMode.LocalProcess => ScoreLocalProcessAsset(assetName, tokens, target),
            ExecutionMode.Docker => ScoreContainerAsset(assetName, tokens, target),
            ExecutionMode.AzureContainer => ScoreContainerAsset(assetName, tokens, target),
            _ => 0,
        };
    }

    private static int ScoreLocalProcessAsset(
        string assetName,
        AssetNameTokens tokens,
        SymphonyReleaseAssetSelectionTarget target)
    {
        if (tokens.HasContainerMarker || !LooksLikeRuntimeArchive(assetName))
        {
            return 0;
        }

        return 100 + ArchivePreferenceScore(assetName, target.OperatingSystem);
    }

    private static int ScoreContainerAsset(
        string assetName,
        AssetNameTokens tokens,
        SymphonyReleaseAssetSelectionTarget target)
    {
        if (target.OperatingSystem is not ReleaseAssetOperatingSystem.Linux)
        {
            return 0;
        }

        if (tokens.HasContainerMarker)
        {
            return 200 + ArchivePreferenceScore(assetName, target.OperatingSystem);
        }

        return LooksLikeRuntimeArchive(assetName)
            ? 100 + ArchivePreferenceScore(assetName, target.OperatingSystem)
            : 0;
    }

    private static int ArchivePreferenceScore(string assetName, ReleaseAssetOperatingSystem operatingSystem)
    {
        string normalized = assetName.ToLowerInvariant();

        if (operatingSystem is ReleaseAssetOperatingSystem.Windows && normalized.EndsWith(".zip", StringComparison.Ordinal))
        {
            return 4;
        }

        if (normalized.EndsWith(".tar.gz", StringComparison.Ordinal))
        {
            return 3;
        }

        if (normalized.EndsWith(".tgz", StringComparison.Ordinal))
        {
            return 2;
        }

        return normalized.EndsWith(".zip", StringComparison.Ordinal) ? 1 : 0;
    }

    private static bool LooksLikeRuntimeArchive(string assetName)
    {
        string normalized = assetName.ToLowerInvariant();

        return normalized.EndsWith(".zip", StringComparison.Ordinal) ||
            normalized.EndsWith(".tar.gz", StringComparison.Ordinal) ||
            normalized.EndsWith(".tgz", StringComparison.Ordinal);
    }

    private static bool IsChecksumAsset(string assetName)
    {
        string normalized = assetName.ToLowerInvariant();

        return normalized.EndsWith(".sha256", StringComparison.Ordinal) ||
            normalized.EndsWith(".sha512", StringComparison.Ordinal) ||
            normalized.EndsWith(".md5", StringComparison.Ordinal) ||
            normalized.EndsWith(".sig", StringComparison.Ordinal) ||
            normalized.EndsWith(".asc", StringComparison.Ordinal);
    }

    private static SymphonyReleaseAsset? FindChecksumAsset(
        IEnumerable<SymphonyReleaseAsset> assets,
        SymphonyReleaseAsset selectedAsset) =>
        assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, $"{selectedAsset.Name}.sha256", StringComparison.OrdinalIgnoreCase)) ??
        assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, $"{selectedAsset.Name}.sha512", StringComparison.OrdinalIgnoreCase));

    private sealed record ScoredAsset(SymphonyReleaseAsset Asset, int Score);

    private sealed class AssetNameTokens
    {
        private static readonly char[] TokenSeparators =
        [
            ' ',
            '.',
            '-',
            '_',
            '/',
            '\\',
            ':',
            '+',
            '(',
            ')',
            '[',
            ']',
        ];

        private readonly HashSet<string> tokens;

        private AssetNameTokens(IEnumerable<string> tokens)
        {
            this.tokens = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public bool HasContainerMarker =>
            tokens.Contains("container") ||
            tokens.Contains("docker") ||
            tokens.Contains("image") ||
            tokens.Contains("oci");

        public static AssetNameTokens Parse(string assetName) =>
            new(assetName.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        public bool Matches(ReleaseAssetOperatingSystem operatingSystem) =>
            operatingSystem switch
            {
                ReleaseAssetOperatingSystem.Windows => tokens.Contains("win") || tokens.Contains("windows"),
                ReleaseAssetOperatingSystem.Linux => tokens.Contains("linux"),
                ReleaseAssetOperatingSystem.MacOS => tokens.Contains("osx") || tokens.Contains("macos") || tokens.Contains("mac") || tokens.Contains("darwin"),
                _ => false,
            };

        public bool Matches(ReleaseAssetArchitecture architecture) =>
            architecture switch
            {
                ReleaseAssetArchitecture.X64 => tokens.Contains("x64") || tokens.Contains("x86") && tokens.Contains("64") || tokens.Contains("x86_64") || tokens.Contains("amd64"),
                ReleaseAssetArchitecture.Arm64 => tokens.Contains("arm64") || tokens.Contains("aarch64"),
                _ => false,
            };
    }
}
