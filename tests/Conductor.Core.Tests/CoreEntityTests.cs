using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;

namespace Conductor.Core.Tests;

public sealed class CoreEntityTests
{
    [Fact]
    public void Project_Captures_Registry_Fields_And_Updates_Details()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var updatedAt = createdAt.AddMinutes(10);
        var project = new Project(
            ProjectId.New(),
            " Platform ",
            " ReleasedGroup ",
            " Internal tooling ",
            " main-only ",
            ProjectStatus.Active,
            createdAt,
            createdAt);

        project.Rename(" Delivery Platform ", " Product Ops ", updatedAt);
        project.UpdateDetails(" Controls Symphony fleets ", " release/* allowed ", updatedAt.AddMinutes(1));

        Assert.Equal("Delivery Platform", project.Name);
        Assert.Equal("Product Ops", project.OwnerName);
        Assert.Equal("Controls Symphony fleets", project.Description);
        Assert.Equal("release/* allowed", project.DefaultBranchPolicy);
        Assert.Equal(updatedAt.AddMinutes(1), project.UpdatedAtUtc);
    }

    [Fact]
    public void Repository_Refreshes_Metadata_And_Project_Assignment()
    {
        var initialProjectId = ProjectId.New();
        var nextProjectId = ProjectId.New();
        var syncedAt = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var repository = new Repository(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "TheConductor",
            "main",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            RepositoryVisibility.Public,
            isArchived: false,
            projectId: initialProjectId,
            lastSyncedAtUtc: null,
            RepositoryOrchestrationStatus.Eligible,
            orchestrationStatusReason: null);

        repository.AssignToProject(nextProjectId);
        repository.RefreshMetadata(
            "trunk",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            RepositoryVisibility.Private,
            isArchived: false,
            syncedAt);

        Assert.Equal(nextProjectId, repository.ProjectId);
        Assert.Equal("trunk", repository.DefaultBranch);
        Assert.Equal(RepositoryVisibility.Private, repository.Visibility);
        Assert.Equal(syncedAt, repository.LastSyncedAtUtc);
    }

    [Fact]
    public void Repository_Rejects_Archived_Eligible_Repository()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Repository(
                RepositoryId.New(),
                RepositoryProvider.GitHub,
                "ReleasedGroup",
                "TheConductor",
                "main",
                new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
                new Uri("https://github.com/ReleasedGroup/TheConductor"),
                RepositoryVisibility.Public,
                isArchived: true,
                projectId: null,
                lastSyncedAtUtc: null,
                RepositoryOrchestrationStatus.Eligible,
                orchestrationStatusReason: null));

        Assert.Contains("Archived repositories", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SymphonyInstance_Captures_Provisioning_Metadata_And_Credentials()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var startedAt = createdAt.AddMinutes(5);
        var gitHubSecretId = SecretId.New();
        var openAiSecretId = SecretId.New();
        var instance = new SymphonyInstance(
            SymphonyInstanceId.New(),
            RepositoryId.New(),
            "ReleasedGroup/TheConductor",
            ExecutionMode.Docker,
            new Uri("http://localhost:8080"),
            createdAt,
            port: 8080,
            containerName: " conductor-symphony-releasedgroup-theconductor ",
            symphonyVersion: " 1.2.3 ",
            symphonyReleaseTag: " v1.2.3 ",
            symphonyArtifactSourceUrl: new Uri("https://github.com/releasedgroup/symphony/releases/tag/v1.2.3"),
            symphonyArtifactChecksum: " sha256:abc123 ",
            gitHubCredentialSecretId: gitHubSecretId,
            gitHubCredentialInheritanceMode: CredentialInheritanceMode.SpecificSecret,
            openAiCredentialSecretId: openAiSecretId,
            openAiCredentialInheritanceMode: CredentialInheritanceMode.SpecificSecret,
            workflowPath: " data/instances/id/config/WORKFLOW.md ",
            dataPath: " data/instances/id/symphony-data ");

        instance.RecordStarted(startedAt);

        Assert.Equal(8080, instance.Port);
        Assert.Equal("conductor-symphony-releasedgroup-theconductor", instance.ContainerName);
        Assert.Equal("1.2.3", instance.SymphonyVersion);
        Assert.Equal("v1.2.3", instance.SymphonyReleaseTag);
        Assert.Equal("sha256:abc123", instance.SymphonyArtifactChecksum);
        Assert.Equal(gitHubSecretId, instance.GitHubCredentialSecretId);
        Assert.Equal(openAiSecretId, instance.OpenAiCredentialSecretId);
        Assert.Equal("data/instances/id/config/WORKFLOW.md", instance.WorkflowPath);
        Assert.Equal("data/instances/id/symphony-data", instance.DataPath);
        Assert.Equal(startedAt, instance.LastStartedAtUtc);
        Assert.Equal(InstanceLifecycleStatus.Running, instance.LifecycleStatus);
    }

    [Fact]
    public void SymphonyInstance_Requires_Secret_Id_For_Specific_Credential_Mode()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new SymphonyInstance(
                SymphonyInstanceId.New(),
                RepositoryId.New(),
                "ReleasedGroup/TheConductor",
                ExecutionMode.LocalProcess,
                new Uri("http://localhost:8080"),
                DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
                gitHubCredentialInheritanceMode: CredentialInheritanceMode.SpecificSecret));

        Assert.Contains("requires a secret id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SymphonyInstance_Rejects_Invalid_Port()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SymphonyInstance(
                SymphonyInstanceId.New(),
                RepositoryId.New(),
                "ReleasedGroup/TheConductor",
                ExecutionMode.LocalProcess,
                new Uri("http://localhost:8080"),
                DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
                port: 70000));

        Assert.Equal("port", exception.ParamName);
    }
}
