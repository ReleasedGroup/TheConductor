using System.Net;
using System.Net.Http.Json;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conductor.Api.Tests;

public sealed class InstanceCredentialAssignmentEndpointTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
    private static readonly DateTimeOffset AssignedAtUtc = DateTimeOffset.Parse("2026-04-29T01:00:00Z");

    [Fact]
    public async Task AssignCredentials_Updates_Instance_Credential_References()
    {
        await using CredentialsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        SeededPortfolio portfolio = await SeedPortfolioAsync(factory.Services);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/instances/{portfolio.InstanceId}/credentials",
            new
            {
                gitHubCredential = new
                {
                    inheritanceMode = "SpecificSecret",
                    secretId = portfolio.GitHubSecretId.ToString(),
                },
                openAiCredential = new
                {
                    inheritanceMode = "None",
                },
                requestedByUserId = "api-user",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CredentialAssignmentResponse? body =
            await response.Content.ReadFromJsonAsync<CredentialAssignmentResponse>();

        Assert.NotNull(body);
        Assert.Equal("SpecificSecret", body.GitHubCredential.InheritanceMode);
        Assert.Equal("None", body.OpenAiCredential.InheritanceMode);
        Assert.Equal("Repository GitHub token", body.GitHubCredential.SecretName);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        SymphonyInstance savedInstance = await dbContext.SymphonyInstances.SingleAsync();

        Assert.Equal(portfolio.GitHubSecretId, savedInstance.GitHubCredentialSecretId);
        Assert.Equal(CredentialInheritanceMode.SpecificSecret, savedInstance.GitHubCredentialInheritanceMode);
        Assert.Null(savedInstance.OpenAiCredentialSecretId);
        Assert.Equal(CredentialInheritanceMode.None, savedInstance.OpenAiCredentialInheritanceMode);
        Assert.Equal("api-user", (await dbContext.AuditEvents.SingleAsync()).ActorUserId);
    }

    [Fact]
    public async Task AssignCredentials_Returns_ValidationProblem_For_Missing_Specific_Secret()
    {
        await using CredentialsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        SeededPortfolio portfolio = await SeedPortfolioAsync(factory.Services);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/instances/{portfolio.InstanceId}/credentials",
            new
            {
                gitHubCredential = new
                {
                    inheritanceMode = "SpecificSecret",
                },
                openAiCredential = new
                {
                    inheritanceMode = "InheritDefault",
                },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        Assert.Equal(0, await dbContext.AuditEvents.CountAsync());
    }

    private static async Task<SeededPortfolio> SeedPortfolioAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();
        SecretId gitHubSecretId = SecretId.New();

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
                CreatedAtUtc));
        await dbContext.SaveChangesAsync();

        return new SeededPortfolio(instanceId, gitHubSecretId);
    }

    private sealed class CredentialsApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();
            using (ConductorDbContext dbContext = CreateDbContext())
            {
                dbContext.Database.EnsureCreated();
            }

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Conductor:BootstrapDevelopmentDatabase"] = "false",
                    ["InstanceCollector:Enabled"] = "false",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ConductorDbContext>();
                services.RemoveAll<DbContextOptions<ConductorDbContext>>();
                services.RemoveAll<IDbContextFactory<ConductorDbContext>>();
                services.RemoveAll<TimeProvider>();

                services.AddDbContext<ConductorDbContext>(options => options.UseSqlite(connection));
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(AssignedAtUtc));
            });
        }

        private ConductorDbContext CreateDbContext()
        {
            DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
                .UseSqlite(connection)
                .Options;

            return new ConductorDbContext(options);
        }

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record SeededPortfolio(
        SymphonyInstanceId InstanceId,
        SecretId GitHubSecretId);

    private sealed record CredentialAssignmentResponse(
        CredentialSummaryResponse GitHubCredential,
        CredentialSummaryResponse OpenAiCredential,
        DateTimeOffset AssignedAtUtc);

    private sealed record CredentialSummaryResponse(
        string InheritanceMode,
        string? SecretId,
        string? SecretName);
}
