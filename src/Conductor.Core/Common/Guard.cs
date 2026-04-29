namespace Conductor.Core.Common;

internal static class Guard
{
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
}
