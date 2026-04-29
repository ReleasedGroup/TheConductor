namespace Conductor.Core.Common;

internal static class Guard
{
    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty identifier is required.", parameterName);
        }

        return value;
    }

    public static string NotWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }

    public static string? OptionalTrimmed(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static Uri AbsoluteUri(Uri value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute URI is required.", parameterName);
        }

        return value;
    }

    public static Uri? OptionalAbsoluteUri(Uri? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        return AbsoluteUri(value, parameterName);
    }

    public static int Positive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A positive value is required.");
        }

        return value;
    }

    public static int NonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A non-negative value is required.");
        }

        return value;
    }

    public static long NonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A non-negative value is required.");
        }

        return value;
    }

    public static int? Port(int? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (value is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A TCP port must be between 1 and 65535.");
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A UTC timestamp is required.", parameterName);
        }

        return value;
    }
}
