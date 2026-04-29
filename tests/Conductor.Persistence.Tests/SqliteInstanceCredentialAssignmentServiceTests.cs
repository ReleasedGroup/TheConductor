using Conductor.Core.Application.Instances;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Persistence.Tests;

public sealed class SqliteInstanceCredentialAssignmentServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
    private static readonly DateTimeOffset AssignedAtUtc = DateTimeOffset.Parse("2026-04-29T01:00:00Z");

    [Fact]
    public async Task AssignAsync_Sets_Distinct_GitHub_And_OpenAi_Credentials_And_Audits_The_Change()
    {
        await using ServiceProvider provider = BuildProvider();
        SeededPortfolio portfolio = await SeedPortfolioAsync(provider);
        await using AsyncServiceScope assignmentScope = provider.CreateAsyncScope();
        IInstanceCredentialAssignmentService service =
            assignmentScope.ServiceProvider.GetRequiredService<IInstanceCredentialAssignmentService>();

        InstanceCredentialAssignmentResult result = await service.AssignAsync(new InstanceCredentialAssignmentRequest(
            portfolio.InstanceId,
            new CredentialAssignmentSelection(CredentialInheritanceMode.SpecificSecret, portfolio.GitHubSecretId),
            new CredentialAssignmentSelection(CredentialInheritanceMode.SpecificSecret, portfolio.OpenAiSecretId),
            RequestedByUserId: "operator"));

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        SymphonyInstance savedInstance = await dbContext.SymphonyInstances.SingleAsync();
        AuditEvent auditEvent = await dbContext.AuditEvents.SingleAsync();

        Assert.Equal(portfolio.GitHubSecretId, savedInstance.GitHubCredentialSecretId);
        Assert.Equal(CredentialInheritanceMode.SpecificSecret, savedInstance.GitHubCredentialInheritanceMode);
        Assert.Equal(portfolio.OpenAiSecretId, savedInstance.OpenAiCredentialSecretId);
        Assert.Equal(CredentialInheritanceMode.SpecificSecret, savedInstance.OpenAiCredentialInheritanceMode);
        Assert.Equal("operator", auditEvent.ActorUserId);
        Assert.Equal("AssignSymphonyInstanceCredentials", auditEvent.Action);
        Assert.Equal(AuditEventOutcome.Succeeded, auditEvent.Outcome);
        Assert.Equal("Repository GitHub token", result.GitHubCredential.SecretName);
        Assert.Equal("Instance OpenAI key", result.OpenAiCredential.SecretName);
        Assert.Equal(AssignedAtUtc, result.AssignedAtUtc);
        Assert.DoesNotContain("ghp_", auditEvent.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", auditEvent.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignAsync_Clears_Secret_Ids_When_Instance_Inherits_Or_Disables_Credentials()
    {
        await using ServiceProvider provider = BuildProvider();
        SeededPortfolio portfolio = await SeedPortfolioAsync(provider);
        await using AsyncServiceScope assignmentScope = provider.CreateAsyncScope();
        IInstanceCredentialAssignmentService service =
            assignmentScope.ServiceProvider.GetRequiredService<IInstanceCredentialAssignmentService>();

        await service.AssignAsync(new InstanceCredentialAssignmentRequest(
            portfolio.InstanceId,
            new CredentialAssignmentSelection(CredentialInheritanceMode.SpecificSecret, portfolio.GitHubSecretId),
            new CredentialAssignmentSelection(CredentialInheritanceMode.SpecificSecret, portfolio.OpenAiSecretId)));
        await service.AssignAsync(new InstanceCredentialAssignmentRequest(
            portfolio.InstanceId,
            new CredentialAssignmentSelection(CredentialInheritanceMode.InheritDefault),
            new CredentialAssignmentSelection(CredentialInheritanceMode.None)));

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        SymphonyInstance savedInstance = await dbContext.SymphonyInstances.SingleAsync();

        Assert.Null(savedInstance.GitHubCredentialSecretId);
        Assert.Equal(CredentialInheritanceMode.InheritDefault, savedInstance.GitHubCredentialInheritanceMode);
        Assert.Null(savedInstance.OpenAiCredentialSecretId);
        Assert.Equal(CredentialInheritanceMode.None, savedInstance.OpenAiCredentialInheritanceMode);
    }

    [Fact]
    public async Task AssignAsync_Rejects_Wrong_Secret_Type_And_Incompatible_Scope()
    {
        await using ServiceProvider provider = BuildProvider();
        SeededPortfolio portfolio = await SeedPortfolioAsync(provider);
        await using AsyncServiceScope assignmentScope = provider.CreateAsyncScope();
        IInstanceCredentialAssignmentService service =
            assignmentScope.ServiceProvider.GetRequiredService<IInstanceCredentialAssignmentService>();

        InstanceCredentialAssignmentValidationException error =
            await Assert.ThrowsAsync<InstanceCredentialAssignmentValidationException>(() => service.AssignAsync(
                new InstanceCredentialAssignmentRequest(
                    portfolio.InstanceId,
                    new CredentialAssignmentSelection(CredentialInheritanceMode.SpecificSecret, portfolio.OpenAiSecretId),
                    new CredentialAssignmentSelection(CredentialInheritanceMode.SpecificSecret, portfolio.OtherRepositoryOpenAiSecretId))));

        Assert.Contains("Selected secret must have one of these types: GitHubToken.", error.Errors["gitHubCredential.secretId"]);
        Assert.Contains("Selected secret is scoped to a different project, repository, or instance.", error.Errors["openAiCredential.secretId"]);

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        Assert.Equal(0, await dbContext.AuditEvents.CountAsync());
        Assert.Equal(0, await dbContext.Events.CountAsync());
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            "conductor-credential-assignment-tests",
            Guid.NewGuid().ToString("N"),
            "conductor.db");
        Dictionary<string, string?> values = new()
        {
            [$"ConnectionStrings:{SqlitePersistenceOptions.ConnectionStringName}"] = $"Data Source={databasePath};Cache=Shared",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        services.AddSingleton<TimeProvider>(new FixedTimeProvider(AssignedAtUtc));
        services.AddConductorPersistence(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<SeededPortfolio> SeedPortfolioAsync(IServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();

        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        RepositoryId otherRepositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();
        SecretId gitHubSecretId = SecretId.New();
        SecretId openAiSecretId = SecretId.New();
        SecretId otherRepositoryOpenAiSecretId = SecretId.New();

        dbContext.AddRange(
            new Project(
                projectId,
                "Platform",
                "ReleasedGroup",
                "Internal orchestration",
                "main",
                ProjectStatus.Active,
                CreatedAtUtc,
                CreatedAtUtc),
            new Repository(
                repositoryId,
                RepositoryProvider.GitHub,
                "ReleasedGroup",
                "TheConductor",
                "main",
                new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
                new Uri("https://github.com/ReleasedGroup/TheConductor"),
                RepositoryVisibility.Public,
                isArchived: false,
                projectId,
                CreatedAtUtc,
                RepositoryOrchestrationStatus.Eligible,
                orchestrationStatusReason: null),
            new Repository(
                otherRepositoryId,
                RepositoryProvider.GitHub,
                "ReleasedGroup",
                "Other",
                "main",
                new Uri("https://github.com/ReleasedGroup/Other.git"),
                new Uri("https://github.com/ReleasedGroup/Other"),
                RepositoryVisibility.Public,
                isArchived: false,
                projectId,
                CreatedAtUtc,
                RepositoryOrchestrationStatus.Eligible,
                orchestrationStatusReason: null),
            new SymphonyInstance(
                instanceId,
                repositoryId,
                "TheConductor main",
                ExecutionMode.Docker,
                new Uri("http://localhost:8080"),
                CreatedAtUtc),
            new SecretDescriptor(
                gitHubSecretId,
                "Repository GitHub token",
                SecretType.GitHubToken,
                SecretScopeType.Repository,
                repositoryId.ToString(),
                CreatedAtUtc),
            new SecretDescriptor(
                openAiSecretId,
                "Instance OpenAI key",
                SecretType.OpenAiApiKey,
                SecretScopeType.SymphonyInstance,
                instanceId.ToString(),
                CreatedAtUtc),
            new SecretDescriptor(
                otherRepositoryOpenAiSecretId,
                "Other OpenAI key",
                SecretType.OpenAiApiKey,
                SecretScopeType.Repository,
                otherRepositoryId.ToString(),
                CreatedAtUtc));
        await dbContext.SaveChangesAsync();

        return new SeededPortfolio(
            instanceId,
            gitHubSecretId,
            openAiSecretId,
            otherRepositoryOpenAiSecretId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record SeededPortfolio(
        SymphonyInstanceId InstanceId,
        SecretId GitHubSecretId,
        SecretId OpenAiSecretId,
        SecretId OtherRepositoryOpenAiSecretId);
}
