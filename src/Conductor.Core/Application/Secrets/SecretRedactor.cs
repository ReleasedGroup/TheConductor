using System.Text.RegularExpressions;
using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Domain.Secrets;

namespace Conductor.Core.Application.Secrets;

public sealed partial class SecretRedactor : ISecretRedactor
{
    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string redacted = AuthorizationHeaderRegex().Replace(value, ReplaceCapturedSecret);
        redacted = EnvironmentSecretRegex().Replace(redacted, ReplaceCapturedSecret);
        redacted = GitHubPersonalAccessTokenRegex().Replace(redacted, SecretTypeMetadata.MaskedDisplayValue);
        redacted = OpenAiApiKeyRegex().Replace(redacted, SecretTypeMetadata.MaskedDisplayValue);

        return redacted;
    }

    private static string ReplaceCapturedSecret(Match match)
    {
        return match.Groups["prefix"].Value + SecretTypeMetadata.MaskedDisplayValue;
    }

    [GeneratedRegex(
        @"(?<prefix>\bAuthorization\b[""']?\s*[:=]\s*[""']?(?:(?:Bearer|Basic|Token)\s+)?)(?<secret>[^\s,""';}]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(
        @"(?<prefix>\b(?:GITHUB_TOKEN|OPENAI_API_KEY)\b[""']?\s*[:=]\s*[""']?)(?<secret>[^\s,""';}]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentSecretRegex();

    [GeneratedRegex(
        @"\b(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex GitHubPersonalAccessTokenRegex();

    [GeneratedRegex(
        @"\bsk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{20,}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex OpenAiApiKeyRegex();
}
