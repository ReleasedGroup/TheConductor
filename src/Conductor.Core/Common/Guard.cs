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

    public static Uri AbsoluteUri(Uri value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute URI is required.", parameterName);
        }

        return value;
    }
}
