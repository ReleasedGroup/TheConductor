using Conductor.Core.Common;

namespace Conductor.Core.Abstractions.Releases;

public sealed record ReleaseSelector
{
    private ReleaseSelector(string? tag)
    {
        Tag = tag;
    }

    public static ReleaseSelector Latest { get; } = new((string?)null);

    public string? Tag { get; }

    public bool IsLatest => Tag is null;

    public static ReleaseSelector Pinned(string tag) => new(Guard.NotWhiteSpace(tag, nameof(tag)));
}
