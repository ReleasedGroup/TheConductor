namespace Conductor.Core.Domain.Time;

public sealed record UtcDateTimeRange
{
    public UtcDateTimeRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = RequireUtc(startUtc, nameof(startUtc));
        EndUtc = RequireUtc(endUtc, nameof(endUtc));

        if (EndUtc < StartUtc)
        {
            throw new ArgumentException("A date/time range cannot end before it starts.", nameof(endUtc));
        }
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    public bool Contains(DateTimeOffset value)
    {
        DateTimeOffset valueUtc = RequireUtc(value, nameof(value));

        return valueUtc >= StartUtc && valueUtc <= EndUtc;
    }

    public bool Overlaps(UtcDateTimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return StartUtc <= other.EndUtc && EndUtc >= other.StartUtc;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A UTC date/time value with zero offset is required.", parameterName);
        }

        return value;
    }
}
