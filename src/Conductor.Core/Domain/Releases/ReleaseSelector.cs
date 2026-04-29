namespace Conductor.Core.Domain.Releases;

public sealed record ReleaseSelector
{
    private ReleaseSelector(ReleaseTag? tag)
    {
        Tag = tag;
    }

    public static ReleaseSelector Latest { get; } = new(tag: null);

    public bool IsLatest => Tag is null;

    public ReleaseTag? Tag { get; }

    public static ReleaseSelector PinnedTag(string tag) => PinnedTag(new ReleaseTag(tag));

    public static ReleaseSelector PinnedTag(ReleaseTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        return new ReleaseSelector(tag);
    }

    public override string ToString() => Tag?.Value ?? "latest";
}
