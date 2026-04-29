using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Application.Secrets;
using Conductor.Core.Domain.Secrets;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Queries;

internal sealed class SqliteSecretDescriptorQueryService(ConductorDbContext dbContext) : ISecretDescriptorQueryService
{
    public async Task<IReadOnlyList<SecretDescriptorView>> ListAsync(
        SecretQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SecretDescriptor> descriptors = dbContext.SecretDescriptors.AsNoTracking();

        if (query.SecretType is { } secretType)
        {
            descriptors = descriptors.Where(descriptor => descriptor.SecretType == secretType);
        }

        if (query.ScopeType is { } scopeType)
        {
            descriptors = descriptors.Where(descriptor => descriptor.ScopeType == scopeType);
        }

        List<SecretDescriptor> results = await descriptors
            .OrderBy(descriptor => descriptor.SecretType)
            .ThenBy(descriptor => descriptor.Name)
            .ToListAsync(cancellationToken);

        return results
            .Select(SecretDescriptorView.FromDescriptor)
            .ToArray();
    }
}
