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

public sealed class ManualInstanceRegistrationEndpointTests
{
    [Fact]
    public async Task Register_Creates_Instance_And_Initial_Snapshot()
    {
        FakeSymphonyApiClient symphonyApiClient = FakeSymphonyApiClient.Healthy();
        await using RegistrationApiFactory factory = new(symphonyApiClient);
        using HttpClient client = factory.CreateClient();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        long initialInstanceCount = await CountAsync(dbContext, "SymphonyInstances");
        long initialSnapshotCount = await CountAsync(dbContext, "InstanceSnapshots");
        long initialEventCount = await CountAsync(dbContext, "Events");
        long initialAuditEventCount = await CountAsync(dbContext, "AuditEvents");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/instances/register",
            new { baseUrl = "http://localhost:5173", displayName = "Billing Symphony" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegistrationResponse>();

        Assert.NotNull(body);
        Assert.Equal("Billing Symphony", body.DisplayName);
        Assert.Equal("Healthy", body.HealthStatus);
        Assert.Equal("ReleasedGroup/BillingApi", body.RepositoryFullName);
        Assert.Equal(1, symphonyApiClient.HealthCallCount);
        Assert.Equal(1, symphonyApiClient.RuntimeCallCount);
        Assert.Equal(1, symphonyApiClient.StateCallCount);

        Assert.Equal(initialInstanceCount + 1, await CountAsync(dbContext, "SymphonyInstances"));
        Assert.Equal(initialSnapshotCount + 1, await CountAsync(dbContext, "InstanceSnapshots"));
        Assert.Equal(initialEventCount + 1, await CountAsync(dbContext, "Events"));
        Assert.Equal(initialAuditEventCount + 1, await CountAsync(dbContext, "AuditEvents"));
    }

    [Fact]
    public async Task Register_Returns_ValidationProblem_And_Does_Not_Write_When_Health_Fails()
    {
        await using RegistrationApiFactory factory = new(FakeSymphonyApiClient.Offline());
        using HttpClient client = factory.CreateClient();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        long initialInstanceCount = await CountAsync(dbContext, "SymphonyInstances");
        long initialSnapshotCount = await CountAsync(dbContext, "InstanceSnapshots");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/instances/register",
            new { baseUrl = "http://localhost:5173" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(initialInstanceCount, await CountAsync(dbContext, "SymphonyInstances"));
        Assert.Equal(initialSnapshotCount, await CountAsync(dbContext, "InstanceSnapshots"));
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

    private sealed class RegistrationApiFactory(FakeSymphonyApiClient symphonyApiClient)
        : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ConductorDbContext>();
                services.RemoveAll<DbContextOptions<ConductorDbContext>>();
                services.RemoveAll<IDbContextFactory<ConductorDbContext>>();
                services.RemoveAll<ISymphonyApiClient>();

                services.AddDbContext<ConductorDbContext>(options => options.UseSqlite(connection));
                services.AddSingleton<ISymphonyApiClient>(symphonyApiClient);
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class FakeSymphonyApiClient : ISymphonyApiClient
    {
        private readonly SymphonyHealthResponse healthResponse;

        private FakeSymphonyApiClient(SymphonyHealthResponse healthResponse)
        {
            this.healthResponse = healthResponse;
        }

        public int HealthCallCount { get; private set; }

        public int RuntimeCallCount { get; private set; }

        public int StateCallCount { get; private set; }

        public static FakeSymphonyApiClient Healthy()
        {
            return new FakeSymphonyApiClient(new SymphonyHealthResponse(
                InstanceHealthStatus.Healthy,
                200,
                TimeSpan.FromMilliseconds(12),
                """{"status":"healthy"}"""));
        }

        public static FakeSymphonyApiClient Offline()
        {
            return new FakeSymphonyApiClient(new SymphonyHealthResponse(
                InstanceHealthStatus.Offline,
                503,
                TimeSpan.FromMilliseconds(1),
                """{"status":"offline"}"""));
        }

        public Task<SymphonyHealthResponse> GetHealthAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            HealthCallCount++;

            return Task.FromResult(healthResponse);
        }

        public Task<SymphonyRuntimeResponse> GetRuntimeAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            RuntimeCallCount++;

            return Task.FromResult(new SymphonyRuntimeResponse(
                """
                {
                  "version": "3.1.4",
                  "instanceId": "symphony-billing-api",
                  "workflow": {
                    "repository": {
                      "owner": "ReleasedGroup",
                      "name": "BillingApi",
                      "defaultBranch": "main",
                      "cloneUrl": "https://github.com/ReleasedGroup/BillingApi.git",
                      "webUrl": "https://github.com/ReleasedGroup/BillingApi"
                    }
                  },
                  "executionMode": "Docker"
                }
                """));
        }

        public Task<SymphonyStateResponse> GetStateAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            StateCallCount++;

            return Task.FromResult(new SymphonyStateResponse("""{"runningSessions":[{"issueIdentifier":"31"}]}"""));
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

    private sealed record RegistrationResponse(
        string DisplayName,
        string HealthStatus,
        string RepositoryFullName);
}
