using Conductor.Core.Abstractions.GitHub;
using Conductor.Core.Application.Repositories;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;

namespace Conductor.Core.Tests;

public sealed class RepositoryImportModelTests
{
    [Fact]
    public void RepositoryImportPlan_Derives_GitHub_Metadata_And_Instance_Shell()
    {
        ProjectId projectId = ProjectId.New();
        RepositoryImportPlan plan = RepositoryImportPlan.Create(new RepositoryImportRequest(
            " ReleasedGroup / TheConductor ",
            " trunk ",
            Visibility: RepositoryVisibility.Private,
            ProjectId: projectId,
            CreateSymphonyInstance: true,
            InstanceDisplayName: " Main Conductor ",
            ExecutionMode: ExecutionMode.Docker,
            InstanceBaseUrl: "http://localhost:8080/",
            ReleaseTag: " v1.2.3 ",
            GitHubCredentialInheritanceMode: CredentialInheritanceMode.None,
            OpenAiCredentialInheritanceMode: CredentialInheritanceMode.InheritDefault,
            WorkflowPath: " ./instances/theconductor/WORKFLOW.md "));

        Assert.Equal("ReleasedGroup/TheConductor", plan.FullName.Value);
        Assert.Equal("trunk", plan.DefaultBranch);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor.git"), plan.CloneUrl);
        Assert.Equal(new Uri("https://github.com/ReleasedGroup/TheConductor"), plan.WebUrl);
        Assert.Equal(RepositoryVisibility.Private, plan.Visibility);
        Assert.Equal(projectId, plan.ProjectId);
        Assert.Equal(RepositoryOrchestrationStatus.Eligible, plan.OrchestrationStatus);

        Assert.NotNull(plan.InstancePlan);
        Assert.Equal("Main Conductor", plan.InstancePlan.DisplayName);
        Assert.Equal(ExecutionMode.Docker, plan.InstancePlan.ExecutionMode);
        Assert.Equal(8080, plan.InstancePlan.Port);
        Assert.Equal("v1.2.3", plan.InstancePlan.ReleaseSelector.ToString());
        Assert.Equal(CredentialInheritanceMode.None, plan.InstancePlan.GitHubCredential.InheritanceMode);
        Assert.Equal("./instances/theconductor/WORKFLOW.md", plan.InstancePlan.WorkflowPath);
    }

    [Fact]
    public void RepositoryImportRequest_FromGitHubRepositorySummary_Copies_Discovered_Metadata()
    {
        ProjectId projectId = ProjectId.New();
        GitHubRepositorySummary repository = new(
            "ReleasedGroup",
            "TheConductor",
            "trunk",
            new Uri("https://github.com/ReleasedGroup/TheConductor.git"),
            new Uri("https://github.com/ReleasedGroup/TheConductor"),
            RepositoryVisibility.Internal,
            IsArchived: true);

        RepositoryImportRequest request = RepositoryImportRequest.FromGitHubRepositorySummary(
            repository,
            projectId);

        Assert.Equal("ReleasedGroup/TheConductor", request.RepositoryFullName);
        Assert.Equal("trunk", request.DefaultBranch);
        Assert.Equal("https://github.com/ReleasedGroup/TheConductor.git", request.CloneUrl);
        Assert.Equal("https://github.com/ReleasedGroup/TheConductor", request.WebUrl);
        Assert.Equal(RepositoryVisibility.Internal, request.Visibility);
        Assert.True(request.IsArchived);
        Assert.Equal(projectId, request.ProjectId);
        Assert.False(request.CreateSymphonyInstance);
    }

    [Fact]
    public void RepositoryImportPlan_Rejects_Invalid_Repository_And_Instance_Inputs()
    {
        RepositoryImportValidationException exception = Assert.Throws<RepositoryImportValidationException>(() =>
            RepositoryImportPlan.Create(new RepositoryImportRequest(
                "not-a-full-name",
                CreateSymphonyInstance: true,
                InstanceBaseUrl: "not-a-url",
                Port: 70000,
                GitHubCredentialInheritanceMode: CredentialInheritanceMode.SpecificSecret,
                IsArchived: true)));

        Assert.Contains(nameof(RepositoryImportRequest.RepositoryFullName), exception.Errors.Keys);
        Assert.Contains(nameof(RepositoryImportRequest.InstanceBaseUrl), exception.Errors.Keys);
        Assert.Contains(nameof(RepositoryImportRequest.Port), exception.Errors.Keys);
        Assert.Contains(nameof(RepositoryImportRequest.GitHubCredentialSecretId), exception.Errors.Keys);
        Assert.Contains(nameof(RepositoryImportRequest.CreateSymphonyInstance), exception.Errors.Keys);
    }

    [Fact]
    public void RepositoryImportPlan_Treats_Blank_Release_As_Latest()
    {
        RepositoryImportPlan plan = RepositoryImportPlan.Create(new RepositoryImportRequest(
            "ReleasedGroup/TheConductor",
            CreateSymphonyInstance: true,
            InstanceBaseUrl: "https://conductor.local/",
            ReleaseTag: "   "));

        Assert.NotNull(plan.InstancePlan);
        Assert.True(plan.InstancePlan.ReleaseSelector.IsLatest);
        Assert.Equal("latest", plan.InstancePlan.ReleaseSelector.ToString());
    }
}
