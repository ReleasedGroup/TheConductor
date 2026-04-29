using Conductor.Core.Common;

namespace Conductor.Core.Domain.Releases;

public sealed record ReleaseTag
{
    public ReleaseTag(string value)
    {
        Value = Guard.NotWhiteSpace(value, nameof(value));
    }

    public string Value { get; }

    public static ReleaseTag Parse(string value) => new(value);

    public override string ToString() => Value;
}
