namespace Conductor.Core.Domain.Secrets;

public sealed record SecretTypeDisplay(
    SecretType SecretType,
    string Label,
    string EnvironmentVariableName,
    string MaskedDisplayValue);

public static class SecretTypeMetadata
{
    public const string MaskedDisplayValue = "************";

    public static IReadOnlyList<SecretTypeDisplay> CredentialTypes { get; } =
    [
        Get(SecretType.GitHubToken),
        Get(SecretType.OpenAiApiKey),
    ];

    public static SecretTypeDisplay Get(SecretType secretType)
    {
        return secretType switch
        {
            SecretType.GitHubToken => new SecretTypeDisplay(
                secretType,
                "GitHub PAT",
                "GITHUB_TOKEN",
                MaskedDisplayValue),
            SecretType.OpenAiApiKey => new SecretTypeDisplay(
                secretType,
                "OpenAI API key",
                "OPENAI_API_KEY",
                MaskedDisplayValue),
            SecretType.CodexHome => new SecretTypeDisplay(
                secretType,
                "Codex home",
                "CODEX_HOME",
                MaskedDisplayValue),
            SecretType.Other => new SecretTypeDisplay(
                secretType,
                "Other secret",
                "Custom",
                MaskedDisplayValue),
            _ => throw new ArgumentOutOfRangeException(nameof(secretType), secretType, "Unsupported secret type."),
        };
    }
}
