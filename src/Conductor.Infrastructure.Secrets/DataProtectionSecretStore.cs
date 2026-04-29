using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Secrets;

public sealed class DataProtectionSecretStore(
    ConductorDbContext dbContext,
    DataProtectionSecretProtector secretProtector,
    TimeProvider timeProvider) : ISecretStore
{
    public async Task<SecretDescriptor> CreateAsync(
        CreateSecretRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string protectedValue = secretProtector.Protect(request.Value);
        DateTimeOffset now = timeProvider.GetUtcNow();
        var descriptor = new SecretDescriptor(
            SecretId.New(),
            request.Name,
            request.SecretType,
            request.ScopeType,
            request.ScopeId,
            now);
        var encryptedValue = new EncryptedSecretValue(descriptor.Id, protectedValue, now);

        dbContext.SecretDescriptors.Add(descriptor);
        dbContext.EncryptedSecretValues.Add(encryptedValue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return descriptor;
    }

    public async Task RotateAsync(
        SecretId secretId,
        RotateSecretRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SecretDescriptor descriptor = await FindDescriptorAsync(secretId, cancellationToken);
        EncryptedSecretValue encryptedValue = await FindEncryptedValueAsync(secretId, cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();
        encryptedValue.Rotate(secretProtector.Protect(request.Value), now);
        descriptor.MarkRotated(now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResolvedSecret> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken)
    {
        if (reference.InheritanceMode is not CredentialInheritanceMode.SpecificSecret)
        {
            throw new InvalidOperationException("Only specific secret references can be resolved directly.");
        }

        EncryptedSecretValue encryptedValue = await dbContext.EncryptedSecretValues
            .AsNoTracking()
            .SingleOrDefaultAsync(secret => secret.SecretId == reference.SecretId, cancellationToken)
            ?? throw new KeyNotFoundException($"Secret '{reference.SecretId}' was not found.");

        return new ResolvedSecret(
            reference.SecretId,
            secretProtector.Unprotect(encryptedValue.ProtectedValue));
    }

    public async Task<ResolvedSecret?> ResolveAsync(
        SecretResolutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<SecretDescriptor> descriptors = await dbContext.SecretDescriptors
            .AsNoTracking()
            .Where(secret => secret.SecretType == request.SecretType)
            .ToListAsync(cancellationToken);
        SecretDescriptor? resolvedDescriptor = SecretResolutionPolicy.Resolve(request, descriptors);

        if (resolvedDescriptor is null)
        {
            return null;
        }

        EncryptedSecretValue encryptedValue = await dbContext.EncryptedSecretValues
            .AsNoTracking()
            .SingleOrDefaultAsync(secret => secret.SecretId == resolvedDescriptor.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Encrypted value for secret '{resolvedDescriptor.Id}' was not found.");

        return new ResolvedSecret(
            resolvedDescriptor.Id,
            secretProtector.Unprotect(encryptedValue.ProtectedValue));
    }

    public async Task<IReadOnlyList<SecretDescriptor>> ListAsync(
        SecretQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<SecretDescriptor> descriptors = dbContext.SecretDescriptors.AsNoTracking();

        if (query.SecretType is { } secretType)
        {
            descriptors = descriptors.Where(secret => secret.SecretType == secretType);
        }

        if (query.ScopeType is { } scopeType)
        {
            descriptors = descriptors.Where(secret => secret.ScopeType == scopeType);
        }

        return await descriptors
            .OrderBy(secret => secret.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(SecretId secretId, CancellationToken cancellationToken)
    {
        SecretDescriptor? descriptor = await dbContext.SecretDescriptors.FindAsync([secretId], cancellationToken);

        if (descriptor is null)
        {
            return;
        }

        EncryptedSecretValue? encryptedValue = await dbContext.EncryptedSecretValues.FindAsync(
            [secretId],
            cancellationToken);

        if (encryptedValue is not null)
        {
            dbContext.EncryptedSecretValues.Remove(encryptedValue);
        }

        dbContext.SecretDescriptors.Remove(descriptor);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SecretDescriptor> FindDescriptorAsync(
        SecretId secretId,
        CancellationToken cancellationToken) =>
        await dbContext.SecretDescriptors.FindAsync([secretId], cancellationToken)
            ?? throw new KeyNotFoundException($"Secret '{secretId}' was not found.");

    private async Task<EncryptedSecretValue> FindEncryptedValueAsync(
        SecretId secretId,
        CancellationToken cancellationToken) =>
        await dbContext.EncryptedSecretValues.FindAsync([secretId], cancellationToken)
            ?? throw new KeyNotFoundException($"Encrypted value for secret '{secretId}' was not found.");
}
