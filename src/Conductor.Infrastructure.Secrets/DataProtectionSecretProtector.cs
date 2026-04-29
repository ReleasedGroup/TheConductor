using Microsoft.AspNetCore.DataProtection;

namespace Conductor.Infrastructure.Secrets;

public sealed class DataProtectionSecretProtector
{
    private const string Purpose = "Conductor.Infrastructure.Secrets.SecretStore.v1";

    private readonly IDataProtector protector;

    public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A secret value is required.", nameof(value));
        }

        return protector.Protect(value);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            throw new ArgumentException("A protected secret value is required.", nameof(protectedValue));
        }

        return protector.Unprotect(protectedValue);
    }
}
