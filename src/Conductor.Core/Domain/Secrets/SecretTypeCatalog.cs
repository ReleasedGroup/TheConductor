using System.Text.Json;
using Conductor.Core.Common;

namespace Conductor.Core.Domain.Secrets;

public sealed record SecretTypeMetadata(
    SecretType SecretType,
    string DisplayName,
    string? RuntimeEnvironmentVariable,
    string MaskedDisplay,
    int MinimumLength,
    IReadOnlyList<string> AcceptedPrefixes)
{
    public bool SupportsValueValidation => AcceptedPrefixes.Count > 0;
}

public sealed record SecretValueValidationResult(
    SecretValidationStatus Status,
    string Message,
    string MetadataJson);

public static class SecretTypeCatalog
{
    private static readonly SecretTypeMetadata GitHubPat = new(
        SecretType.GitHubPersonalAccessToken,
        "GitHub PAT",
        "GITHUB_TOKEN",
        "github_pat_********",
        MinimumLength: 20,
        ["github_pat_", "ghp_"]);

    private static readonly SecretTypeMetadata OpenAiApiKey = new(
        SecretType.OpenAiApiKey,
        "OpenAI API key",
        "OPENAI_API_KEY",
        "sk-********",
        MinimumLength: 20,
        ["sk-"]);

    private static readonly SecretTypeMetadata CodexHome = new(
        SecretType.CodexHome,
        "Codex home",
        null,
        "configured",
        MinimumLength: 1,
        []);

    private static readonly SecretTypeMetadata Other = new(
        SecretType.Other,
        "Secret",
        null,
        "********",
        MinimumLength: 1,
        []);

    public static SecretTypeMetadata GetMetadata(SecretType secretType) =>
        secretType switch
        {
            SecretType.GitHubPersonalAccessToken => GitHubPat,
            SecretType.OpenAiApiKey => OpenAiApiKey,
            SecretType.CodexHome => CodexHome,
            SecretType.Other => Other,
            _ => Other,
        };

    public static SecretValueValidationResult ValidateValue(SecretType secretType, string value)
    {
        string normalized = Guard.NotWhiteSpace(value, nameof(value));
        SecretTypeMetadata metadata = GetMetadata(secretType);
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
                $"{metadata.DisplayName} format looks valid.",
                metadataJson);
        }

        return new SecretValueValidationResult(
            SecretValidationStatus.Invalid,
            $"{metadata.DisplayName} must use an accepted prefix and be at least {metadata.MinimumLength} characters long.",
            metadataJson);
    }

    private static string CreateValidationMetadataJson(SecretTypeMetadata metadata) =>
        JsonSerializer.Serialize(new SecretValidationMetadataDocument(
            metadata.SecretType.ToString(),
            metadata.RuntimeEnvironmentVariable,
            metadata.AcceptedPrefixes));

    private sealed record SecretValidationMetadataDocument(
        string SecretType,
        string? RuntimeEnvironmentVariable,
        IReadOnlyList<string> AcceptedPrefixes);
}
