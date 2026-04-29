namespace Conductor.Core.Abstractions.Secrets;

public interface ISecretRedactor
{
    string Redact(string value);
}
