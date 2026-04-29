using Conductor.Core.Abstractions.Secrets;

namespace Conductor.Core.Application.Secrets;

public interface ISecretDescriptorQueryService
{
    Task<IReadOnlyList<SecretDescriptorView>> ListAsync(
        SecretQuery query,
        CancellationToken cancellationToken = default);
}
