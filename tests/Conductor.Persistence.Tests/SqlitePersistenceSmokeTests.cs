using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Snapshots;
using Conductor.Core.Domain.Symphony;
using Conductor.Core.Domain.SymphonyReleases;
using Conductor.Core.Domain.Workflows;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Persistence.Tests;

public sealed class SqlitePersistenceSmokeTests
{
    [Fact]
    public async Task DbContext_Creates_Configured_Sqlite_Tables()
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using ConductorDbContext dbContext = new(options);

        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Assert.True(await dbContext.Database.CanConnectAsync());

        IReadOnlySet<string> tableNames = await ReadTableNamesAsync(dbContext);

        Assert.Contains("Projects", tableNames);
        Assert.Contains("Repositories", tableNames);
        Assert.Contains("SymphonyInstances", tableNames);
        Assert.Contains("WorkflowProfiles", tableNames);
        Assert.Contains("SymphonyReleaseArtifacts", tableNames);
        Assert.Contains("InstanceSnapshots", tableNames);
        Assert.Contains("SecretDescriptors", tableNames);
    }

    [Fact]
    public async Task DbContext_Persists_Domain_Entities_With_Configured_Converters()
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using ConductorDbContext dbContext = new(options);

        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.Parse("2026-04-29T01:00:00Z");
        ProjectId projectId = ProjectId.New();
        RepositoryId repositoryId = RepositoryId.New();
        SymphonyInstanceId instanceId = SymphonyInstanceId.New();
        WorkflowProfileId workflowProfileId = WorkflowProfileId.New();
        SecretId secretId = SecretId.New();

        var project = new Project(projectId, "Platform", "ReleasedGroup", ProjectStatus.Active, now, now);
        var repository = new Repository(
            repositoryId,
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "TheConductor",
            "main",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            isArchived: false,
            projectId);
        var instance = new SymphonyInstance(
            instanceId,
            repositoryId,
            "TheConductor main",
            ExecutionMode.Docker,
            new Uri("http://localhost:8080"),
            InstanceLifecycleStatus.Running,
            InstanceHealthStatus.Healthy);
        var workflowProfile = new WorkflowProfile(
            workflowProfileId,
            "Default",
            "# WORKFLOW",
            now);
        var releaseArtifact = new SymphonyReleaseArtifact(
            "v1.2.3",
            "symphony-linux-x64.zip",
            new Uri("https://example.com/releases/v1.2.3/symphony-linux-x64.zip"),
            now,
            "sha256:abc");
        var snapshot = new InstanceSnapshot(
            instanceId,
            now.AddMinutes(5),
            InstanceHealthStatus.Healthy,
            """{"status":"ok"}""",
            """{"version":"1.2.3"}""",
            """{"running":1}""");
        var secret = new SecretDescriptor(
            secretId,
            "Repository GitHub token",
            SecretType.GitHubToken,
            SecretScopeType.Repository,
            repositoryId.ToString(),
            now,
            RotatedAtUtc: null);

        dbContext.AddRange(project, repository, instance, workflowProfile, releaseArtifact, snapshot, secret);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        Repository savedRepository = await dbContext.Repositories.SingleAsync();
        SymphonyInstance savedInstance = await dbContext.SymphonyInstances.SingleAsync();
        InstanceSnapshot savedSnapshot = await dbContext.InstanceSnapshots.SingleAsync();
        SecretDescriptor savedSecret = await dbContext.SecretDescriptors.SingleAsync();

        Assert.Equal(projectId, savedRepository.ProjectId);
        Assert.Equal("ReleasedGroup/TheConductor", savedRepository.FullName);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor.git"), savedRepository.CloneUrl);
        Assert.Equal(ExecutionMode.Docker, savedInstance.ExecutionMode);
        Assert.Equal(InstanceLifecycleStatus.Running, savedInstance.LifecycleStatus);
        Assert.Equal(InstanceHealthStatus.Healthy, savedSnapshot.HealthStatus);
        Assert.Equal("""{"running":1}""", savedSnapshot.StateJson);
        Assert.Equal(secretId, savedSecret.Id);
        Assert.Equal(SecretScopeType.Repository, savedSecret.ScopeType);
    }

    private static async Task<IReadOnlySet<string>> ReadTableNamesAsync(ConductorDbContext dbContext)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        HashSet<string> tableNames = new(StringComparer.Ordinal);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }
}
