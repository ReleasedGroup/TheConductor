using Conductor.Core.Common;

namespace Conductor.Core.Domain.Repositories;

public sealed record GitHubRepositoryFullName
{
    public GitHubRepositoryFullName(string owner, string name)
    {
        Owner = NormalizeSegment(owner, nameof(owner));
        Name = NormalizeSegment(name, nameof(name));
    }

    public string Owner { get; }

    public string Name { get; }

    public string Value => $"{Owner}/{Name}";

    public static GitHubRepositoryFullName Parse(string value)
    {
        string normalized = Guard.NotWhiteSpace(value, nameof(value));
        string[] parts = normalized.Split('/', StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw new ArgumentException("A GitHub repository full name must be in owner/name format.", nameof(value));
        }

        return new GitHubRepositoryFullName(parts[0], parts[1]);
    }

    public static bool TryParse(string? value, out GitHubRepositoryFullName? fullName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fullName = null;
            return false;
        }

        string[] parts = value.Trim().Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && IsValidSegment(parts[0]) && IsValidSegment(parts[1]))
        {
            fullName = new GitHubRepositoryFullName(parts[0], parts[1]);
            return true;
        }

        fullName = null;
        return false;
    }

    public override string ToString() => Value;

    private static string NormalizeSegment(string value, string parameterName)
    {
        string normalized = Guard.NotWhiteSpace(value, parameterName);

        if (!IsValidSegment(normalized))
        {
            throw new ArgumentException("GitHub repository owner and name segments cannot contain slashes or whitespace.", parameterName);
        }

        return normalized;
    }

    private static bool IsValidSegment(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains('/', StringComparison.Ordinal)
        && !value.Any(char.IsWhiteSpace);
}
