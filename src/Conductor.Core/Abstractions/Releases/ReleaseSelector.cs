using Conductor.Core.Common;

namespace Conductor.Core.Abstractions.Releases;

public sealed record ReleaseSelector
{
    private ReleaseSelector(bool isLatest, string? tag)
    {
        IsLatest = isLatest;
        Tag = tag;
    }

    public static ReleaseSelector Latest { get; } = new(isLatest: true, tag: null);

    public bool IsLatest { get; }

    public string? Tag { get; }

    public static ReleaseSelector PinnedTag(string tag) =>
        new(isLatest: false, tag: Guard.NotWhiteSpace(tag, nameof(tag)));
}
