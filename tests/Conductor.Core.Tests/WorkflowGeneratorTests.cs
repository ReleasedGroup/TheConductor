using Conductor.Core.Application.Workflows;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Repositories;

namespace Conductor.Core.Tests;

public sealed class WorkflowGeneratorTests
{
    [Fact]
    public void Generate_Docker_Workflow_Includes_Docker_Workspace_Layout_And_Required_Fields()
    {
        WorkflowGenerator generator = new();
        WorkflowGenerationRequest request = new(
            new GitHubRepositoryFullName("ReleasedGroup", "TheConductor"),
            ExecutionMode.Docker,
            Port: 18081,
            ProfileSource: "Review open GitHub issues with care.",
            BaseBranch: "main",
            RemoteUrl: "https://github.com/ReleasedGroup/TheConductor.git");

        string workflow = generator.Generate(request);

        Assert.Contains("tracker:", workflow, StringComparison.Ordinal);
        Assert.Contains("owner: \"ReleasedGroup\"", workflow, StringComparison.Ordinal);
        Assert.Contains("repo: \"TheConductor\"", workflow, StringComparison.Ordinal);
        Assert.Contains("api_key: $GITHUB_TOKEN", workflow, StringComparison.Ordinal);
        Assert.Contains("activeStates:", workflow, StringComparison.Ordinal);
        Assert.Contains("terminalStates:", workflow, StringComparison.Ordinal);
        Assert.Contains("polling:", workflow, StringComparison.Ordinal);
        Assert.Contains("agent:", workflow, StringComparison.Ordinal);
        Assert.Contains("codex:", workflow, StringComparison.Ordinal);
        Assert.Contains("server:", workflow, StringComparison.Ordinal);
        Assert.Contains("port: 18081", workflow, StringComparison.Ordinal);
        Assert.Contains("workspace:", workflow, StringComparison.Ordinal);
        Assert.Contains("root: \"/var/lib/symphony/workspaces\"", workflow, StringComparison.Ordinal);
        Assert.Contains("sharedClonePath: \"/var/lib/symphony/workspaces/repo\"", workflow, StringComparison.Ordinal);
        Assert.Contains("worktreesRoot: \"/var/lib/symphony/workspaces/worktrees\"", workflow, StringComparison.Ordinal);
        Assert.Contains("hooks:", workflow, StringComparison.Ordinal);
        Assert.Contains("Review open GitHub issues with care.", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Local_Workflow_Uses_Provided_Host_Workspace_Paths()
    {
        WorkflowGenerator generator = new();
        string workspaceRoot = Path.Combine("build", "workspaces");
        WorkflowGenerationRequest request = new(
            new GitHubRepositoryFullName("ReleasedGroup", "TheConductor"),
            ExecutionMode.LocalProcess,
            Port: 8080,
            WorkspaceRoot: workspaceRoot,
            ProfileSource: "Implement the requested fix.");

        string workflow = generator.Generate(request);

        string expectedSharedClonePath = Path.Combine(workspaceRoot, "repo");
        string expectedWorktreesRoot = Path.Combine(workspaceRoot, "worktrees");

        Assert.Contains($"root: \"{workspaceRoot}\"", workflow, StringComparison.Ordinal);
        Assert.Contains($"sharedClonePath: \"{expectedSharedClonePath}\"", workflow, StringComparison.Ordinal);
        Assert.Contains($"worktreesRoot: \"{expectedWorktreesRoot}\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("/var/lib/symphony/workspaces", workflow, StringComparison.Ordinal);
        Assert.Contains("Implement the requested fix.", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Strips_Profile_Front_Matter_And_Uses_Default_Prompt_When_Missing()
    {
        WorkflowGenerator generator = new();
        string profileSource = """
---
tracker:
  owner: ignored
---
Use the provided issue instructions below.
""";
        WorkflowGenerationRequest request = new(
            new GitHubRepositoryFullName("ReleasedGroup", "TheConductor"),
            ExecutionMode.LocalProcess,
            ProfileSource: profileSource,
            Port: 8080);

        string workflow = generator.Generate(request);

        Assert.Contains("Use the provided issue instructions below.", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("owner: \"ignored\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Uses_Default_Prompt_When_Profile_Is_Blank()
    {
        WorkflowGenerator generator = new();
        WorkflowGenerationRequest request = new(
            new GitHubRepositoryFullName("ReleasedGroup", "TheConductor"),
            ExecutionMode.Docker,
            Port: null,
            ProfileSource: "   ");

        string workflow = generator.Generate(request);

        Assert.Contains("You are working on a repository workflow.", workflow, StringComparison.Ordinal);
        Assert.Contains("port: 8080", workflow, StringComparison.Ordinal);
    }
}
