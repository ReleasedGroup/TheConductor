using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Secrets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Persistence.Tests;

public sealed class SqliteSecretStoreTests
{
    [Fact]
    public async Task CreateAsync_Stores_Descriptor_And_Encrypted_Value_Separately()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);

        SecretDescriptor descriptor = await store.CreateAsync(
            new CreateSecretRequest(
                "Repository GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Repository,
                "repo-123",
                "ghp_test_secret_value"),
            CancellationToken.None);

        SecretDescriptor savedDescriptor = await dbContext.SecretDescriptors.SingleAsync();
        EncryptedSecretValue encryptedValue = await dbContext.EncryptedSecretValues.SingleAsync();

        Assert.Equal(descriptor.Id, savedDescriptor.Id);
        Assert.Equal("Repository GitHub token", savedDescriptor.Name);
        Assert.Equal(SecretValidationStatus.Valid, savedDescriptor.ValidationStatus);
        Assert.NotNull(savedDescriptor.ValidatedAtUtc);
        Assert.Contains("GitHub PAT", savedDescriptor.ValidationMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_test_secret_value", savedDescriptor.ValidationMetadataJson, StringComparison.Ordinal);
        Assert.Equal(descriptor.Id, encryptedValue.SecretId);
        Assert.NotEqual("ghp_test_secret_value", encryptedValue.ProtectedValue);
        Assert.DoesNotContain("ghp_test_secret_value", encryptedValue.ProtectedValue, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(SecretDescriptor).GetProperties(),
            property => property.Name.Contains("Value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveAsync_Returns_Plaintext_Only_For_Specific_Secret_References()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);
        SecretDescriptor descriptor = await store.CreateAsync(
            new CreateSecretRequest(
                "OpenAI API key",
                SecretType.OpenAiApiKey,
                SecretScopeType.Global,
                ScopeId: null,
                "sk-test-secret"),
            CancellationToken.None);

        ResolvedSecret resolved = await store.ResolveAsync(
            new SecretReference(descriptor.Id, CredentialInheritanceMode.SpecificSecret),
            CancellationToken.None);

        Assert.Equal(descriptor.Id, resolved.SecretId);
        Assert.Equal("sk-test-secret", resolved.Value);
        Assert.DoesNotContain("sk-test-secret", resolved.ToString(), StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ResolveAsync(
            new SecretReference(descriptor.Id, CredentialInheritanceMode.InheritDefault),
            CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_Uses_Inherited_Scope_Precedence()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);
        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();

        await store.CreateAsync(
            new CreateSecretRequest(
                "Global GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Global,
                ScopeId: null,
                "ghp_global_secret_value"),
            CancellationToken.None);
        await store.CreateAsync(
            new CreateSecretRequest(
                "Repository GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Repository,
                repositoryId.ToString(),
                "ghp_repository_secret_value"),
            CancellationToken.None);
        SecretDescriptor instanceSecret = await store.CreateAsync(
            new CreateSecretRequest(
                "Instance GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.SymphonyInstance,
                instanceId.ToString(),
                "ghp_instance_secret_value"),
            CancellationToken.None);

        ResolvedSecret? resolved = await store.ResolveAsync(
            new SecretResolutionRequest(
                SecretType.GitHubToken,
                instanceId,
                repositoryId,
                projectId,
                CredentialInheritanceMode.InheritDefault),
            CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(instanceSecret.Id, resolved.SecretId);
        Assert.Equal("ghp_instance_secret_value", resolved.Value);
    }

    [Fact]
    public async Task ResolveAsync_Returns_Null_When_Inheritance_Mode_Is_None()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);

        ResolvedSecret? resolved = await store.ResolveAsync(
            new SecretResolutionRequest(
                SecretType.OpenAiApiKey,
                SymphonyInstanceId.New(),
                RepositoryId.New(),
                projectId: null,
                CredentialInheritanceMode.None),
            CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task RotateAsync_Updates_Metadata_And_Replaces_Encrypted_Value()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);
        SecretDescriptor descriptor = await store.CreateAsync(
            new CreateSecretRequest(
                "Repository GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Repository,
                "repo-123",
                "old-secret"),
            CancellationToken.None);
        string originalProtectedValue = await dbContext.EncryptedSecretValues
            .Where(secret => secret.SecretId == descriptor.Id)
            .Select(secret => secret.ProtectedValue)
            .SingleAsync();

        await store.RotateAsync(descriptor.Id, new RotateSecretRequest("new-secret"), CancellationToken.None);

        SecretDescriptor rotatedDescriptor = await dbContext.SecretDescriptors.SingleAsync();
        EncryptedSecretValue rotatedValue = await dbContext.EncryptedSecretValues.SingleAsync();
        ResolvedSecret resolved = await store.ResolveAsync(
            new SecretReference(descriptor.Id, CredentialInheritanceMode.SpecificSecret),
            CancellationToken.None);

        Assert.NotNull(rotatedDescriptor.RotatedAtUtc);
        Assert.Equal(SecretValidationStatus.Invalid, rotatedDescriptor.ValidationStatus);
        Assert.NotNull(rotatedDescriptor.ValidatedAtUtc);
        Assert.NotNull(rotatedValue.RotatedAtUtc);
        Assert.NotEqual(originalProtectedValue, rotatedValue.ProtectedValue);
        Assert.Equal("new-secret", resolved.Value);
    }

    [Fact]
    public async Task ListAsync_Returns_Descriptors_Without_Resolving_Values()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);
        await store.CreateAsync(
            new CreateSecretRequest(
                "Repository GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Repository,
                "repo-123",
                "github-secret"),
            CancellationToken.None);
        await store.CreateAsync(
            new CreateSecretRequest(
                "Global OpenAI API key",
                SecretType.OpenAiApiKey,
                SecretScopeType.Global,
                ScopeId: null,
                "openai-secret"),
            CancellationToken.None);

        IReadOnlyList<SecretDescriptor> descriptors = await store.ListAsync(
            new SecretQuery(SecretType.GitHubToken),
            CancellationToken.None);

        Assert.Single(descriptors);
        Assert.Equal(SecretType.GitHubToken, descriptors[0].SecretType);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Descriptor_And_Encrypted_Value()
    {
        await using ConductorDbContext dbContext = await CreateDbContextAsync();
        DataProtectionSecretStore store = CreateStore(dbContext);
        SecretDescriptor descriptor = await store.CreateAsync(
            new CreateSecretRequest(
                "Repository GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Repository,
                "repo-123",
                "secret"),
            CancellationToken.None);

        await store.DeleteAsync(descriptor.Id, CancellationToken.None);

        Assert.Empty(await dbContext.SecretDescriptors.ToListAsync());
        Assert.Empty(await dbContext.EncryptedSecretValues.ToListAsync());
    }

    [Fact]
    public void Secret_Request_ToString_Masks_Values()
    {
        var createRequest = new CreateSecretRequest(
            "Repository GitHub token",
            SecretType.GitHubToken,
            SecretScopeType.Repository,
            "repo-123",
            "secret-value");
        var rotateRequest = new RotateSecretRequest("rotated-secret-value");

        Assert.DoesNotContain("secret-value", createRequest.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("rotated-secret-value", rotateRequest.ToString(), StringComparison.Ordinal);
    }

    private static async Task<ConductorDbContext> CreateDbContextAsync()
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new ConductorDbContext(options);

        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }

    private static DataProtectionSecretStore CreateStore(ConductorDbContext dbContext)
    {
        IDataProtectionProvider dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = new DataProtectionSecretProtector(dataProtectionProvider);

        return new DataProtectionSecretStore(dbContext, protector, TimeProvider.System);
    }
}
