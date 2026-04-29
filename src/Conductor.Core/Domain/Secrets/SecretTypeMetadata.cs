using System.Text.Json;
using Conductor.Core.Common;

namespace Conductor.Core.Domain.Secrets;

public sealed record SecretTypeDisplay(
    SecretType SecretType,
    string Label,
    string EnvironmentVariableName,
    string MaskedDisplayValue,
    int MinimumLength,
    IReadOnlyList<string> AcceptedPrefixes)
{
    public bool SupportsValueValidation => AcceptedPrefixes.Count > 0;
}

public sealed record SecretValueValidationResult(
    SecretValidationStatus Status,
    string Message,
    string MetadataJson);

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
                MaskedDisplayValue,
                MinimumLength: 20,
                ["github_pat_", "ghp_"]),
            SecretType.OpenAiApiKey => new SecretTypeDisplay(
                secretType,
                "OpenAI API key",
                "OPENAI_API_KEY",
                MaskedDisplayValue,
                MinimumLength: 20,
                ["sk-"]),
            SecretType.CodexHome => new SecretTypeDisplay(
                secretType,
                "Codex home",
                "CODEX_HOME",
                MaskedDisplayValue,
                MinimumLength: 1,
                []),
            SecretType.Other => new SecretTypeDisplay(
                secretType,
                "Other secret",
                "Custom",
                MaskedDisplayValue,
                MinimumLength: 1,
                []),
            _ => throw new ArgumentOutOfRangeException(nameof(secretType), secretType, "Unsupported secret type."),
        };
    }

    public static SecretValueValidationResult ValidateValue(SecretType secretType, string value)
    {
        string normalized = Guard.NotWhiteSpace(value, nameof(value));
        SecretTypeDisplay metadata = Get(secretType);
        string metadataJson = CreateValidationMetadataJson(metadata);

        if (!metadata.SupportsValueValidation)
        {
            return new SecretValueValidationResult(
                SecretValidationStatus.NotValidated,
                "No local validation rules are defined for this secret type.",
                metadataJson);
        }

        bool hasAcceptedPrefix = metadata.AcceptedPrefixes.Any(
            prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
        bool hasMinimumLength = normalized.Length >= metadata.MinimumLength;

        if (hasAcceptedPrefix && hasMinimumLength)
        {
            return new SecretValueValidationResult(
                SecretValidationStatus.Valid,
                $"{metadata.Label} format looks valid.",
                metadataJson);
        }

        return new SecretValueValidationResult(
            SecretValidationStatus.Invalid,
            $"{metadata.Label} must use an accepted prefix and be at least {metadata.MinimumLength} characters long.",
            metadataJson);
    }

    private static string CreateValidationMetadataJson(SecretTypeDisplay metadata) =>
        JsonSerializer.Serialize(new SecretValidationMetadataDocument(
            metadata.SecretType.ToString(),
            metadata.EnvironmentVariableName,
            metadata.AcceptedPrefixes));

    private sealed record SecretValidationMetadataDocument(
        string SecretType,
        string EnvironmentVariableName,
        IReadOnlyList<string> AcceptedPrefixes);
}
