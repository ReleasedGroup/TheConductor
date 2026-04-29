using System.IO;
using System.Text;
using Conductor.Core.Common;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Repositories;

namespace Conductor.Core.Application.Workflows;

public interface IWorkflowGenerator
{
    string Generate(WorkflowGenerationRequest request);
}

public sealed class WorkflowGenerator : IWorkflowGenerator
{
    private const int DefaultPort = 8080;
    private const string DockerWorkspaceRoot = "/var/lib/symphony/workspaces";
    private const string DockerSharedClonePath = "/var/lib/symphony/workspaces/repo";
    private const string DockerWorktreesRoot = "/var/lib/symphony/workspaces/worktrees";
    private const string DefaultBaseBranch = "main";
    private const string DefaultPrompt = """
You are working on a repository workflow.

Keep execution disciplined.
Use repository signals and prompt instructions to prioritize safe, incremental progress.
""";

    public string Generate(WorkflowGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RepositoryFullName);

        int port = request.Port ?? DefaultPort;
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Port), "Port must be between 1 and 65535.");
        }

        string baseBranch = string.IsNullOrWhiteSpace(request.BaseBranch)
            ? DefaultBaseBranch
            : request.BaseBranch.Trim();

        string remoteUrl = ResolveRemoteUrl(request);
        (string workspaceRoot, string workspaceSharedClonePath, string workspaceWorktreesRoot) =
            ResolveWorkspacePaths(request);

        string prompt = ResolvePrompt(request.ProfileSource);
        StringBuilder workflow = new();

        workflow.AppendLine("---");
        workflow.AppendLine("tracker:");
        workflow.AppendLine($"  kind: github");
        workflow.AppendLine($"  owner: {Quote(request.RepositoryFullName.Owner)}");
        workflow.AppendLine($"  repo: {Quote(request.RepositoryFullName.Name)}");
        workflow.AppendLine("  api_key: $GITHUB_TOKEN");
        workflow.AppendLine("  includePullRequests: true");
        workflow.AppendLine("  activeStates:");
        workflow.AppendLine("    - Open");
        workflow.AppendLine("    - In Progress");
        workflow.AppendLine("  terminalStates:");
        workflow.AppendLine("    - Closed");
        workflow.AppendLine("polling:");
        workflow.AppendLine("  intervalMs: 600000");
        workflow.AppendLine("agent:");
        workflow.AppendLine("  maxConcurrentAgents: 5");
        workflow.AppendLine("  maxTurns: 20");
        workflow.AppendLine("  maxRetryBackoffMs: 300000");
        workflow.AppendLine("  maxConcurrentAgentsByState:");
        workflow.AppendLine("    Open: 2");
        workflow.AppendLine("    In Progress: 3");
        workflow.AppendLine("codex:");
        workflow.AppendLine("  command: codex app-server");
        workflow.AppendLine("  api_key: $OPENAI_API_KEY");
        workflow.AppendLine("  turnTimeoutMs: 3600000");
        workflow.AppendLine("  approvalPolicy: never");
        workflow.AppendLine("  threadSandbox: danger-full-access");
        workflow.AppendLine("  turnSandboxPolicy: danger-full-access");
        workflow.AppendLine("  readTimeoutMs: 5000");
        workflow.AppendLine("  stallTimeoutMs: 300000");
        workflow.AppendLine($"  openaiApiKey: {Quote("$OPENAI_API_KEY")}");
        workflow.AppendLine("server:");
        workflow.AppendLine($"  port: {port}");
        workflow.AppendLine("workspace:");
        workflow.AppendLine($"  root: {Quote(workspaceRoot)}");
        workflow.AppendLine($"  sharedClonePath: {Quote(workspaceSharedClonePath)}");
        workflow.AppendLine($"  worktreesRoot: {Quote(workspaceWorktreesRoot)}");
        workflow.AppendLine($"  baseBranch: {Quote(baseBranch)}");
        workflow.AppendLine($"  remoteUrl: {Quote(remoteUrl)}");
        workflow.AppendLine("hooks:");
        workflow.AppendLine("  hasAfterCreate: false");
        workflow.AppendLine("  hasBeforeRun: true");
        workflow.AppendLine("  hasAfterRun: true");
        workflow.AppendLine("  hasBeforeRemove: false");
        workflow.AppendLine("  beforeRemoveSupported: true");
        workflow.AppendLine("  timeoutMs: 60000");
        workflow.AppendLine("---");
        workflow.AppendLine();
        workflow.AppendLine(prompt);

        return workflow.ToString();
    }

    private static string ResolvePrompt(string? profileSource)
    {
        if (string.IsNullOrWhiteSpace(profileSource))
        {
            return DefaultPrompt;
        }

        ReadOnlySpan<char> source = profileSource.AsSpan().Trim();
        if (!source.StartsWith("---".AsSpan(), StringComparison.Ordinal))
        {
            return source.ToString();
        }

        string normalized = NormalizeLineEndings(source.ToString());
        string[] lines = normalized.Split('\n');
        int markerIndex = -1;

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                markerIndex = i;
                break;
            }
        }

        if (markerIndex < 0 || markerIndex == lines.Length - 1)
        {
            return DefaultPrompt;
        }

        string prompt = string.Join('\n', lines[(markerIndex + 1)..]).Trim();
        return string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt;
    }

    private static string ResolveRemoteUrl(WorkflowGenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RemoteUrl))
        {
            return request.RemoteUrl!.Trim();
        }

        return $"https://github.com/{request.RepositoryFullName.Value}.git";
    }

    private static (string root, string sharedClonePath, string worktreesRoot) ResolveWorkspacePaths(
        WorkflowGenerationRequest request)
    {
        if (request.ExecutionMode == ExecutionMode.Docker)
        {
            return (DockerWorkspaceRoot, DockerSharedClonePath, DockerWorktreesRoot);
        }

        string root = string.IsNullOrWhiteSpace(request.WorkspaceRoot)
            ? Path.Combine(
                "instances",
                request.RepositoryFullName.Owner,
                request.RepositoryFullName.Name,
                "workspaces")
            : request.WorkspaceRoot.Trim();

        string sharedClonePath = Path.Combine(root, "repo");
        string worktreesRoot = Path.Combine(root, "worktrees");

        return (root, sharedClonePath, worktreesRoot);
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.OrdinalIgnoreCase);

    private static string Quote(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}

public sealed record WorkflowGenerationRequest(
    GitHubRepositoryFullName RepositoryFullName,
    ExecutionMode ExecutionMode,
    int? Port = null,
    string? ProfileSource = null,
    string? WorkspaceRoot = null,
    string? BaseBranch = null,
    string? RemoteUrl = null);
