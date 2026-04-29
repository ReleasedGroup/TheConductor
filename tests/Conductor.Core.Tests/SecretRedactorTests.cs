using Conductor.Core.Application.Secrets;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Tests;

public sealed class SecretRedactorTests
{
    private readonly SecretRedactor redactor = new();

    [Fact]
    public void Redact_Removes_GitHub_Personal_Access_Tokens()
    {
        string classicToken = "gh" + "p_" + new string('A', 36);
        string fineGrainedToken = "github_" + "pat_" + new string('B', 24) + "_" + new string('C', 40);
        string content = $"classic={classicToken} fine-grained={fineGrainedToken}";

        string redacted = redactor.Redact(content);

        Assert.DoesNotContain(classicToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(fineGrainedToken, redacted, StringComparison.Ordinal);
        Assert.Equal(2, CountMasks(redacted));
    }

    [Fact]
    public void Redact_Removes_OpenAi_Api_Keys()
    {
        string apiKey = "sk-" + new string('A', 48);
        string projectKey = "sk-" + "proj-" + new string('B', 48);
        string content = $"apiKey={apiKey} projectKey={projectKey}";

        string redacted = redactor.Redact(content);

        Assert.DoesNotContain(apiKey, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(projectKey, redacted, StringComparison.Ordinal);
        Assert.Equal(2, CountMasks(redacted));
    }

    [Fact]
    public void Redact_Removes_Known_Environment_Variable_Values()
    {
        const string gitHubToken = "short-github-token-value";
        const string openAiApiKey = "short-openai-api-key-value";
        string content = $"""
            GITHUB_TOKEN={gitHubToken}
            "OPENAI_API_KEY": "{openAiApiKey}"
            UNRELATED_TOKEN={gitHubToken}
            """;

        string redacted = redactor.Redact(content);

        Assert.Contains($"GITHUB_TOKEN={SecretTypeMetadata.MaskedDisplayValue}", redacted, StringComparison.Ordinal);
        Assert.Contains(
            $"""
            "OPENAI_API_KEY": "{SecretTypeMetadata.MaskedDisplayValue}"
            """,
            redacted,
            StringComparison.Ordinal);
        Assert.Contains($"UNRELATED_TOKEN={gitHubToken}", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(openAiApiKey, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_Removes_Authorization_Header_Values()
    {
        const string bearerToken = "opaque-bearer-header-value";
        const string basicToken = "opaque-basic-header-value";
        const string unrelatedToken = "opaque-non-header-value";
        string content = $"""
            Authorization: Bearer {bearerToken}
            "authorization": "Basic {basicToken}"
            X-Trace-Token: {unrelatedToken}
            """;

        string redacted = redactor.Redact(content);

        Assert.Contains($"Authorization: Bearer {SecretTypeMetadata.MaskedDisplayValue}", redacted, StringComparison.Ordinal);
        Assert.Contains(
            $"""
            "authorization": "Basic {SecretTypeMetadata.MaskedDisplayValue}"
            """,
            redacted,
            StringComparison.Ordinal);
        Assert.DoesNotContain(bearerToken, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(basicToken, redacted, StringComparison.Ordinal);
        Assert.Contains(unrelatedToken, redacted, StringComparison.Ordinal);
    }

    private static int CountMasks(string value)
    {
        return value.Split(SecretTypeMetadata.MaskedDisplayValue).Length - 1;
    }
}
