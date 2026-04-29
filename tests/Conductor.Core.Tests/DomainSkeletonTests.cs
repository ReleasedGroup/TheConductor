using Conductor.Core.Abstractions.Releases;
using Conductor.Core.Application;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Projects;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Symphony;
using Conductor.Infrastructure.GitHub;
using Conductor.Infrastructure.Notifications;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Reporting;
using Conductor.Infrastructure.Runners.Docker;
using Conductor.Infrastructure.Runners.Local;
using Conductor.Infrastructure.Secrets;
using Conductor.Infrastructure.Symphony;

namespace Conductor.Core.Tests;

public sealed class DomainSkeletonTests
{
    [Fact]
    public void Project_Trims_Names_And_Can_Be_Archived()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var archivedAt = createdAt.AddHours(1);
        var project = new Project(ProjectId.New(), "  Conductor  ", "  Platform  ", ProjectStatus.Active, createdAt, createdAt);

        project.Archive(archivedAt);

        Assert.Equal("Conductor", project.Name);
        Assert.Equal("Platform", project.OwnerName);
        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.Equal(archivedAt, project.UpdatedAtUtc);
    }

    [Fact]
    public void Repository_Exposes_GitHub_FullName()
    {
        var repository = new Repository(
            RepositoryId.New(),
            RepositoryProvider.GitHub,
            "ReleasedGroup",
            "TheConductor",
            "main",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            isArchived: false,
            projectId: ProjectId.New());

        Assert.Equal("ReleasedGroup/TheConductor", repository.FullName);
        Assert.False(repository.IsArchived);
    }

    [Fact]
    public void SymphonyInstance_Tracks_Last_Health_Check_Separately_From_Last_Seen()
    {
        var instance = new SymphonyInstance(
            SymphonyInstanceId.New(),
            RepositoryId.New(),
            "Conductor main",
            ExecutionMode.Docker,
            new Uri("http://localhost:8080"));

        var healthyAt = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var offlineAt = healthyAt.AddMinutes(5);

        instance.RecordHealth(InstanceHealthStatus.Healthy, healthyAt);
        instance.RecordHealth(InstanceHealthStatus.Offline, offlineAt);

        Assert.Equal(InstanceHealthStatus.Offline, instance.HealthStatus);
        Assert.Equal(offlineAt, instance.LastHealthCheckAtUtc);
        Assert.Equal(healthyAt, instance.LastSeenAtUtc);
    }

    [Fact]
    public void ReleaseSelector_Latest_Has_No_Pinned_Tag()
    {
        var selector = ReleaseSelector.Latest;

        Assert.True(selector.IsLatest);
        Assert.Null(selector.Tag);
    }

    [Fact]
    public void Infrastructure_Modules_Declare_Project_Boundaries()
    {
        InfrastructureModule[] modules =
        [
            GitHubInfrastructureModule.Descriptor,
            SymphonyInfrastructureModule.Descriptor,
            LocalRunnerInfrastructureModule.Descriptor,
            DockerRunnerInfrastructureModule.Descriptor,
            SecretsInfrastructureModule.Descriptor,
            ReportingInfrastructureModule.Descriptor,
            NotificationsInfrastructureModule.Descriptor,
            SqlitePersistenceInfrastructureModule.Descriptor,
        ];

        Assert.All(modules, module =>
        {
            Assert.StartsWith("Conductor.Infrastructure.", module.Name, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(module.Responsibility));
        });
    }
}
