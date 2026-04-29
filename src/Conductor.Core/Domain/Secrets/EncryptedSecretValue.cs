using Conductor.Core.Common;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Secrets;

public sealed class EncryptedSecretValue
{
    public EncryptedSecretValue(
        SecretId secretId,
        string protectedValue,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? rotatedAtUtc = null)
    {
        SecretId = secretId;
        ProtectedValue = Guard.NotWhiteSpace(protectedValue, nameof(protectedValue));
        CreatedAtUtc = createdAtUtc;
        RotatedAtUtc = rotatedAtUtc;
    }

    public SecretId SecretId { get; }

    public string ProtectedValue { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? RotatedAtUtc { get; private set; }

    public void Rotate(string protectedValue, DateTimeOffset rotatedAtUtc)
    {
        ProtectedValue = Guard.NotWhiteSpace(protectedValue, nameof(protectedValue));
        RotatedAtUtc = rotatedAtUtc;
    }
}
