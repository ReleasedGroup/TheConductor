using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Conductor.Core.Abstractions.Symphony;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conductor.Api.Tests;

public sealed class RepositoryImportEndpointTests
{
    [Fact]
    public async Task Import_Creates_Repository_And_Instance_Shell()
    {
        await using RepositoryImportApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/repos/import",
            new
            {
                repositoryFullName = "ReleasedGroup/TheConductor",
                defaultBranch = "main",
                visibility = "Private",
                createSymphonyInstance = true,
                instanceDisplayName = "Conductor local",
                executionMode = "LocalProcess",
                instanceBaseUrl = "http://localhost:8080/",
                releaseTag = "latest",
                gitHubCredentialInheritanceMode = "None",
                openAiCredentialInheritanceMode = "InheritDefault",
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ImportResponse? body = await response.Content.ReadFromJsonAsync<ImportResponse>();

        Assert.NotNull(body);
        Assert.Equal("ReleasedGroup/TheConductor", body.RepositoryFullName);
        Assert.True(body.CreatedRepository);
        Assert.True(body.CreatedSymphonyInstance);
        Assert.NotNull(body.SymphonyInstanceId);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        Assert.Equal(1, await CountAsync(dbContext, "Repositories"));
        Assert.Equal(1, await CountAsync(dbContext, "SymphonyInstances"));
        Assert.Equal(1, await CountAsync(dbContext, "AuditEvents"));
    }

    [Fact]
    public async Task Import_Returns_ValidationProblem_When_Repository_Name_Is_Invalid()
    {
        await using RepositoryImportApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/repos/import",
            new
            {
                repositoryFullName = "TheConductor",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();

        Assert.Equal(0, await CountAsync(dbContext, "Repositories"));
    }

    private static async Task<long> CountAsync(ConductorDbContext dbContext, string tableName)
    {
        await using DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\";";

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync();
        }

        object? value = await command.ExecuteScalarAsync();

        return Convert.ToInt64(value);
    }

    private sealed class RepositoryImportApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConductorDbContext>>();
                services.RemoveAll<ISymphonyApiClient>();

                services.AddDbContext<ConductorDbContext>(options => options.UseSqlite(connection));
                services.AddSingleton<ISymphonyApiClient>(new NoOpSymphonyApiClient());
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class NoOpSymphonyApiClient : ISymphonyApiClient
    {
        public Task<SymphonyHealthResponse> GetHealthAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymphonyRuntimeResponse> GetRuntimeAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymphonyStateResponse> GetStateAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymphonyWorkflowDocument> GetWorkflowAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymphonyWorkflowDocument> SaveWorkflowAsync(
            Uri baseUri,
            SymphonyWorkflowDocument document,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymphonyIssueResponse?> GetIssueAsync(
            Uri baseUri,
            string issueIdentifier,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymphonyRefreshResponse> RequestRefreshAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record ImportResponse(
        string RepositoryFullName,
        bool CreatedRepository,
        bool CreatedSymphonyInstance,
        string? SymphonyInstanceId);
}
