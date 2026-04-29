using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Tests;

public sealed class Issue39SecretDescriptorTests
{
    [Fact]
    public void GitHub_Pat_Secret_Type_Exposes_Safe_Display_Metadata()
    {
        var descriptor = new SecretDescriptor(
            SecretId.New(),
            "Default GitHub PAT",
            SecretType.GitHubPersonalAccessToken,
            SecretScopeType.Global,
            scopeId: null,
            new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("GitHub PAT", descriptor.TypeDisplayName);
        Assert.Equal("GITHUB_TOKEN", descriptor.RuntimeEnvironmentVariable);
        Assert.Equal(SecretTypeMetadata.MaskedDisplayValue, descriptor.MaskedDisplay);
        Assert.DoesNotContain("Default GitHub PAT", descriptor.MaskedDisplay, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHub_Pat_Value_Validation_Returns_Metadata_Without_Leaking_Secret()
    {
        const string token = "github_pat_abcdefghijklmnopqrstuvwxyz";

        SecretValueValidationResult result = SecretTypeMetadata.ValidateValue(
            SecretType.GitHubPersonalAccessToken,
            token);

        Assert.Equal(SecretValidationStatus.Valid, result.Status);
        Assert.Contains("GitHub PAT", result.Message, StringComparison.Ordinal);
        Assert.Contains("github_pat_", result.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("GITHUB_TOKEN", result.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(token, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(token, result.MetadataJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("github_pat_abcdefghijklmnopqrstuvwxyz")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz")]
    public void GitHub_Pat_Value_Validation_Accepts_Pat_Prefixes(string token)
    {
        SecretValueValidationResult result = SecretTypeMetadata.ValidateValue(
            SecretType.GitHubPersonalAccessToken,
            token);

        Assert.Equal(SecretValidationStatus.Valid, result.Status);
    }

    [Fact]
    public void GitHub_Pat_Value_Validation_Rejects_Non_GitHub_Token()
    {
        SecretValueValidationResult result = SecretTypeMetadata.ValidateValue(
            SecretType.GitHubPersonalAccessToken,
            "sk-abcdefghijklmnopqrstuvwxyz");

        Assert.Equal(SecretValidationStatus.Invalid, result.Status);
    }

    [Fact]
    public void SecretDescriptor_Records_Validation_Metadata()
    {
        var descriptor = new SecretDescriptor(
            SecretId.New(),
            "Repository PAT",
            SecretType.GitHubPersonalAccessToken,
            SecretScopeType.Repository,
            RepositoryId.New().ToString(),
            new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));
        DateTimeOffset validatedAt = new(2026, 4, 29, 1, 0, 0, TimeSpan.Zero);
        SecretValueValidationResult result = SecretTypeMetadata.ValidateValue(
            SecretType.GitHubPersonalAccessToken,
            "ghp_abcdefghijklmnopqrstuvwxyz");

        descriptor.RecordValidation(
            result.Status,
            validatedAt,
            result.Message,
            result.MetadataJson);

        Assert.Equal(SecretValidationStatus.Valid, descriptor.ValidationStatus);
        Assert.Equal(validatedAt, descriptor.ValidatedAtUtc);
        Assert.Equal(result.Message, descriptor.ValidationMessage);
        Assert.Equal(result.MetadataJson, descriptor.ValidationMetadataJson);
    }

    [Fact]
    public void Rotating_Secret_Clears_Validation_Metadata()
    {
        var descriptor = new SecretDescriptor(
            SecretId.New(),
            "Repository PAT",
            SecretType.GitHubPersonalAccessToken,
            SecretScopeType.Repository,
            RepositoryId.New().ToString(),
            new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero),
            validationStatus: SecretValidationStatus.Valid,
            validatedAtUtc: new DateTimeOffset(2026, 4, 29, 1, 0, 0, TimeSpan.Zero),
            validationMessage: "GitHub token permissions verified.",
            validationMetadataJson: """{"scopes":["repo"]}""");

        descriptor.MarkRotated(new DateTimeOffset(2026, 4, 29, 2, 0, 0, TimeSpan.Zero));

        Assert.Equal(SecretValidationStatus.NotValidated, descriptor.ValidationStatus);
        Assert.Null(descriptor.ValidatedAtUtc);
        Assert.Null(descriptor.ValidationMessage);
        Assert.Null(descriptor.ValidationMetadataJson);
    }

    [Fact]
    public void Validation_Metadata_Requires_Utc_Timestamps()
    {
        var descriptor = new SecretDescriptor(
            SecretId.New(),
            "Repository PAT",
            SecretType.GitHubPersonalAccessToken,
            SecretScopeType.Repository,
            RepositoryId.New().ToString(),
            new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));

        var error = Assert.Throws<ArgumentException>(() => descriptor.RecordValidation(
            SecretValidationStatus.Valid,
            new DateTimeOffset(2026, 4, 29, 1, 0, 0, TimeSpan.FromHours(10)),
            "GitHub token permissions verified.",
            "{}"));

        Assert.Equal("validatedAtUtc", error.ParamName);
    }

    [Fact]
    public void Legacy_GitHubToken_Name_Still_Parses_To_GitHub_Pat_Type()
    {
        SecretType parsed = Enum.Parse<SecretType>("GitHubToken");

        Assert.Equal(SecretType.GitHubPersonalAccessToken, parsed);
    }
}
