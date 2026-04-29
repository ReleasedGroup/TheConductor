using System.Globalization;
using System.Text.Json;
using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Application.Repositories;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.Workflows;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Persistence.Tests;

public sealed class SqliteRepositoryImportServiceTests
{
    [Fact]
    public async Task ImportAsync_Creates_Repository_Instance_Shell_And_Audit_Event()
    {
        DateTimeOffset importedAtUtc = DateTimeOffset.Parse("2026-04-29T04:00:00Z");
        await using ServiceProvider provider = BuildProvider(importedAtUtc);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        WorkflowProfileId workflowProfileId = WorkflowProfileId.New();
        dbContext.WorkflowProfiles.Add(new WorkflowProfile(
            workflowProfileId,
            "Default Docker",
            "Default orchestration profile",
            "# WORKFLOW",
            isDefault: true,
            importedAtUtc,
            importedAtUtc));
        await dbContext.SaveChangesAsync();
        IRepositoryImportService importService = scope.ServiceProvider.GetRequiredService<IRepositoryImportService>();

        RepositoryImportResult result = await importService.ImportAsync(new RepositoryImportRequest(
            "ReleasedGroup/TheConductor",
            "main",
            Visibility: RepositoryVisibility.Private,
            CreateSymphonyInstance: true,
            InstanceDisplayName: "Conductor local",
            ExecutionMode: ExecutionMode.LocalProcess,
            InstanceBaseUrl: "http://localhost:8080/",
            ReleaseTag: "latest",
            WorkflowProfileId: workflowProfileId,
            GitHubCredentialInheritanceMode: CredentialInheritanceMode.None,
            OpenAiCredentialInheritanceMode: CredentialInheritanceMode.InheritDefault,
            RequestedByUserId: "nick"));

        Repository repository = await dbContext.Repositories.SingleAsync();
        SymphonyInstance instance = await dbContext.SymphonyInstances.SingleAsync();
        AuditEvent auditEvent = await dbContext.AuditEvents.SingleAsync();

        Assert.True(result.CreatedRepository);
        Assert.True(result.CreatedSymphonyInstance);
        Assert.Equal(repository.Id.ToString(), result.RepositoryId);
        Assert.Equal(instance.Id.ToString(), result.SymphonyInstanceId);
        Assert.Equal("ReleasedGroup/TheConductor", repository.FullName.Value);
        Assert.Equal(RepositoryVisibility.Private, repository.Visibility);
        Assert.Equal(importedAtUtc, repository.LastSyncedAtUtc);
        Assert.Equal("Conductor local", instance.DisplayName);
        Assert.Equal(InstanceLifecycleStatus.NotProvisioned, instance.LifecycleStatus);
        Assert.Equal(InstanceHealthStatus.Unknown, instance.HealthStatus);
        Assert.Equal("latest", instance.SymphonyReleaseTag);
        Assert.Equal(workflowProfileId, instance.WorkflowProfileId);
        Assert.Equal(CredentialInheritanceMode.None, instance.GitHubCredentialInheritanceMode);
        Assert.Equal("ImportRepository", auditEvent.Action);
        Assert.Equal("nick", auditEvent.ActorUserId);
        Assert.Equal("Repository", auditEvent.TargetResourceType);
        Assert.Equal(repository.Id.ToString(), auditEvent.TargetResourceId);
        Assert.Equal(importedAtUtc, auditEvent.OccurredAtUtc);
        Assert.Equal(AuditEventOutcome.Succeeded, auditEvent.Outcome);
        Assert.Equal("Imported repository ReleasedGroup/TheConductor.", auditEvent.Message);

        using JsonDocument auditMetadata = JsonDocument.Parse(auditEvent.MetadataJson!);
        JsonElement metadata = auditMetadata.RootElement;

        Assert.Equal("ReleasedGroup/TheConductor", metadata.GetProperty("repositoryFullName").GetString());
        Assert.True(metadata.GetProperty("createdRepository").GetBoolean());
        Assert.True(metadata.GetProperty("createSymphonyInstance").GetBoolean());
        Assert.True(metadata.GetProperty("createdSymphonyInstance").GetBoolean());
        Assert.Equal(instance.Id.ToString(), metadata.GetProperty("symphonyInstanceId").GetString());
        Assert.Equal(workflowProfileId.ToString(), metadata.GetProperty("workflowProfileId").GetString());
    }

    [Fact]
    public async Task ImportAsync_Persists_Discovered_Repository_Metadata()
    {
        DateTimeOffset importedAtUtc = DateTimeOffset.Parse("2026-04-29T04:00:00Z");
        await using ServiceProvider provider = BuildProvider(importedAtUtc);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        IRepositoryImportService importService = scope.ServiceProvider.GetRequiredService<IRepositoryImportService>();

        GitHubRepositorySummary discoveredRepository = new(
            "ReleasedGroup",
            "TheConductor",
            "trunk",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            RepositoryVisibility.Internal,
            IsArchived: true);

        RepositoryImportResult result = await importService.ImportAsync(
            RepositoryImportRequest.FromGitHubRepositorySummary(discoveredRepository) with
            {
                RequestedByUserId = "nick",
            });

        Repository repository = await dbContext.Repositories.SingleAsync();

        Assert.True(result.CreatedRepository);
        Assert.False(result.CreatedSymphonyInstance);
        Assert.Equal("ReleasedGroup/TheConductor", result.RepositoryFullName);
        Assert.Equal("ReleasedGroup", repository.Owner);
        Assert.Equal("TheConductor", repository.Name);
        Assert.Equal("trunk", repository.DefaultBranch);
        Assert.Equal(discoveredRepository.CloneUrl, repository.CloneUrl);
        Assert.Equal(discoveredRepository.WebUrl, repository.WebUrl);
        Assert.Equal(RepositoryVisibility.Internal, repository.Visibility);
        Assert.True(repository.IsArchived);
        Assert.Equal(importedAtUtc, repository.LastSyncedAtUtc);
        Assert.Equal(RepositoryOrchestrationStatus.Ineligible, repository.OrchestrationStatus);
        Assert.Equal("Archived repositories cannot be orchestrated.", repository.OrchestrationStatusReason);
    }

    [Fact]
    public async Task ImportAsync_Updates_Existing_Repository_And_Does_Not_Duplicate_Instance_Shell()
    {
        DateTimeOffset importedAtUtc = DateTimeOffset.Parse("2026-04-29T04:00:00Z");
        await using ServiceProvider provider = BuildProvider(importedAtUtc);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        IRepositoryImportService importService = scope.ServiceProvider.GetRequiredService<IRepositoryImportService>();

        var request = new RepositoryImportRequest(
            "ReleasedGroup/TheConductor",
            "main",
            CreateSymphonyInstance: true,
            InstanceDisplayName: "Conductor local",
            InstanceBaseUrl: "http://localhost:8080/",
            GitHubCredentialInheritanceMode: CredentialInheritanceMode.None,
            OpenAiCredentialInheritanceMode: CredentialInheritanceMode.None);

        RepositoryImportResult firstResult = await importService.ImportAsync(request);
        RepositoryImportResult secondResult = await importService.ImportAsync(request with
        {
            DefaultBranch = "trunk",
            Visibility = RepositoryVisibility.Internal,
        });

        Repository repository = await dbContext.Repositories.SingleAsync();

        Assert.True(firstResult.CreatedRepository);
        Assert.False(secondResult.CreatedRepository);
        Assert.True(firstResult.CreatedSymphonyInstance);
        Assert.False(secondResult.CreatedSymphonyInstance);
        Assert.Equal("trunk", repository.DefaultBranch);
        Assert.Equal(RepositoryVisibility.Internal, repository.Visibility);
        Assert.Equal(1, await dbContext.SymphonyInstances.CountAsync());
        Assert.Equal(2, await dbContext.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_Assigns_Selected_Project_And_Returns_Project_Metadata()
    {
        DateTimeOffset importedAtUtc = DateTimeOffset.Parse("2026-04-29T04:00:00Z");
        await using ServiceProvider provider = BuildProvider(importedAtUtc);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        ProjectId projectId = ProjectId.New();
        dbContext.Projects.Add(new Project(
            projectId,
            "Platform",
            "ReleasedGroup",
            description: null,
            "main",
            ProjectStatus.Active,
            importedAtUtc,
            importedAtUtc));
        await dbContext.SaveChangesAsync();
        IRepositoryImportService importService = scope.ServiceProvider.GetRequiredService<IRepositoryImportService>();

        RepositoryImportResult result = await importService.ImportAsync(new RepositoryImportRequest(
            "ReleasedGroup/TheConductor",
            ProjectId: projectId));

        Repository repository = await dbContext.Repositories.SingleAsync();
        AuditEvent auditEvent = await dbContext.AuditEvents.SingleAsync();
        using JsonDocument metadata = JsonDocument.Parse(auditEvent.MetadataJson!);

        Assert.Equal(projectId, repository.ProjectId);
        Assert.Equal(projectId.ToString(), result.ProjectId);
        Assert.Equal("Platform", result.ProjectName);
        Assert.Equal(projectId.ToString(), metadata.RootElement.GetProperty("projectId").GetString());
        Assert.Equal("Platform", metadata.RootElement.GetProperty("projectName").GetString());
    }

    [Fact]
    public async Task ImportAsync_Rejects_Missing_Project_Reference_Without_Writing()
    {
        DateTimeOffset importedAtUtc = DateTimeOffset.Parse("2026-04-29T04:00:00Z");
        await using ServiceProvider provider = BuildProvider(importedAtUtc);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        IRepositoryImportService importService = scope.ServiceProvider.GetRequiredService<IRepositoryImportService>();

        RepositoryImportValidationException exception = await Assert.ThrowsAsync<RepositoryImportValidationException>(() =>
            importService.ImportAsync(new RepositoryImportRequest(
                "ReleasedGroup/TheConductor",
                ProjectId: Conductor.Core.Domain.Ids.ProjectId.New())));

        Assert.Contains(nameof(RepositoryImportRequest.ProjectId), exception.Errors.Keys);
        Assert.Equal(0, await dbContext.Repositories.CountAsync());
        Assert.Equal(0, await dbContext.AuditEvents.CountAsync());
    }

    private static ServiceProvider BuildProvider(DateTimeOffset utcNow)
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            "conductor-repository-import-tests",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "conductor.db");
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlitePersistenceOptions.ConnectionStringName}"] = $"Data Source={databasePath};Cache=Shared",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(utcNow));
        services.AddConductorPersistence(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
