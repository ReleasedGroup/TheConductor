using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Secrets;

public enum SecretScopeType
{
    Global,
    Project,
    Repository,
    SymphonyInstance,
}

public enum SecretType
{
    GitHubToken,
    GitHubPersonalAccessToken = GitHubToken,

    OpenAiApiKey,
    CodexHome,
    Other,
}

public enum SecretValidationStatus
{
    NotValidated,
    Valid,
    Invalid,
}

public sealed class SecretDescriptor
{
    public SecretDescriptor(
        SecretId id,
        string name,
        SecretType secretType,
        SecretScopeType scopeType,
        string? scopeId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? rotatedAtUtc = null,
        SecretValidationStatus validationStatus = SecretValidationStatus.NotValidated,
        DateTimeOffset? validatedAtUtc = null,
        string? validationMessage = null,
        string? validationMetadataJson = null)
    {
        Id = id;
        Name = Guard.NotWhiteSpace(name, nameof(name));
        SecretType = secretType;
        ScopeType = scopeType;
        ScopeId = NormalizeScopeId(scopeType, scopeId);
        CreatedAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        RotatedAtUtc = NormalizeOptionalUtc(rotatedAtUtc, nameof(rotatedAtUtc));
        ValidationStatus = validationStatus;
        ValidatedAtUtc = NormalizeValidationTimestamp(validationStatus, validatedAtUtc);
        ValidationMessage = Guard.OptionalTrimmed(validationMessage);
        ValidationMetadataJson = Guard.OptionalTrimmed(validationMetadataJson);
    }

    public SecretId Id { get; }

    public string Name { get; }

    public SecretType SecretType { get; }

    public SecretScopeType ScopeType { get; }

    public string? ScopeId { get; }

    public string TypeDisplayName => SecretTypeMetadata.Get(SecretType).Label;

    public string RuntimeEnvironmentVariable => SecretTypeMetadata.Get(SecretType).EnvironmentVariableName;

    public string MaskedDisplay => SecretTypeMetadata.Get(SecretType).MaskedDisplayValue;

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? RotatedAtUtc { get; private set; }

    public SecretValidationStatus ValidationStatus { get; private set; }

    public DateTimeOffset? ValidatedAtUtc { get; private set; }

    public string? ValidationMessage { get; private set; }

    public string? ValidationMetadataJson { get; private set; }

    public void MarkRotated(DateTimeOffset rotatedAtUtc)
    {
        RotatedAtUtc = Guard.Utc(rotatedAtUtc, nameof(rotatedAtUtc));
        ValidationStatus = SecretValidationStatus.NotValidated;
        ValidatedAtUtc = null;
        ValidationMessage = null;
        ValidationMetadataJson = null;
    }

    public void RecordValidation(
        SecretValidationStatus validationStatus,
        DateTimeOffset validatedAtUtc,
        string? validationMessage,
        string? validationMetadataJson)
    {
        if (validationStatus is SecretValidationStatus.NotValidated)
        {
            throw new ArgumentException("A validation result must be valid or invalid.", nameof(validationStatus));
        }

        ValidationStatus = validationStatus;
        ValidatedAtUtc = Guard.Utc(validatedAtUtc, nameof(validatedAtUtc));
        ValidationMessage = Guard.OptionalTrimmed(validationMessage);
        ValidationMetadataJson = Guard.OptionalTrimmed(validationMetadataJson);
    }

    private static string? NormalizeScopeId(SecretScopeType scopeType, string? scopeId)
    {
        if (scopeType is SecretScopeType.Global)
        {
            if (!string.IsNullOrWhiteSpace(scopeId))
            {
                throw new ArgumentException("Global secrets cannot have a scope identifier.", nameof(scopeId));
            }

            return null;
        }

        return Guard.NotWhiteSpace(scopeId ?? string.Empty, nameof(scopeId));
    }

    private static DateTimeOffset? NormalizeOptionalUtc(DateTimeOffset? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        return Guard.Utc(value.Value, parameterName);
    }

    private static DateTimeOffset? NormalizeValidationTimestamp(
        SecretValidationStatus validationStatus,
        DateTimeOffset? validatedAtUtc)
    {
        if (validationStatus is SecretValidationStatus.NotValidated)
        {
            if (validatedAtUtc is not null)
            {
                throw new ArgumentException(
                    "Unvalidated secrets cannot have a validation timestamp.",
                    nameof(validatedAtUtc));
            }

            return null;
        }

        if (validatedAtUtc is null)
        {
            throw new ArgumentException("Validated secrets require a validation timestamp.", nameof(validatedAtUtc));
        }

        return Guard.Utc(validatedAtUtc.Value, nameof(validatedAtUtc));
    }
}
