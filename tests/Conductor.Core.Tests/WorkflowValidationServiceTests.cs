using Conductor.Core.Application.Workflows;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.Secrets;
using Conductor.Core.Domain.Workflows;

namespace Conductor.Core.Tests;

public sealed class WorkflowValidationServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2026-04-29T00:00:00Z");

    [Fact]
    public void Validate_Accepts_Docker_Workflow_With_Resolved_Required_Secrets()
    {
        WorkflowValidationService service = new();

        WorkflowValidationResult result = service.Validate(ValidRequest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_Rejects_Missing_Provisioning_Settings()
    {
        WorkflowValidationService service = new();
        WorkflowValidationRequest request = ValidRequest() with
        {
            Port = null,
            WorkflowPath = "config/workflow.txt",
            DataPath = "   ",
        };

        WorkflowValidationResult result = service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(WorkflowValidationRequest.Port), result.Errors.Keys);
        Assert.Contains(nameof(WorkflowValidationRequest.WorkflowPath), result.Errors.Keys);
        Assert.Contains(nameof(WorkflowValidationRequest.DataPath), result.Errors.Keys);
    }

    [Fact]
    public void Validate_Rejects_Workflow_Source_That_Misses_Repository_Or_Secret_Environment_Reference()
    {
        WorkflowValidationService service = new();
        WorkflowValidationRequest request = ValidRequest() with
        {
            WorkflowProfile = Profile(
                """
                tracker:
                  owner: OtherOrg
                  repo: OtherRepo
                  api_key: github_pat_literal-secret
                """),
        };

        WorkflowValidationResult result = service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(WorkflowValidationRequest.WorkflowProfile), result.Errors.Keys);
        Assert.Contains(nameof(WorkflowProfile.WorkflowSource), result.Errors.Keys);
    }

    [Fact]
    public void Validate_Rejects_Disabled_Or_Unresolved_Required_Secret_References()
    {
        WorkflowValidationService service = new();
        WorkflowValidationRequest request = ValidRequest() with
        {
            SecretReferences =
            [
                new WorkflowSecretReference(SecretType.GitHubToken, CredentialInheritanceMode.None),
                new WorkflowSecretReference(SecretType.OpenAiApiKey, CredentialInheritanceMode.InheritDefault),
            ],
        };

        WorkflowValidationResult result = service.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("SecretReferences.GitHubToken", result.Errors.Keys);
        Assert.Contains("SecretReferences.OpenAiApiKey", result.Errors.Keys);
    }

    [Fact]
    public void WorkflowSecretReference_Requires_Secret_Id_Only_For_Specific_Mode()
    {
        var missingSecretError = Assert.Throws<ArgumentException>(() =>
            new WorkflowSecretReference(SecretType.GitHubToken, CredentialInheritanceMode.SpecificSecret));
        var inheritedSecretError = Assert.Throws<ArgumentException>(() =>
            new WorkflowSecretReference(
                SecretType.GitHubToken,
                CredentialInheritanceMode.InheritDefault,
                SecretId.New()));

        Assert.Equal("secretId", missingSecretError.ParamName);
        Assert.Equal("secretId", inheritedSecretError.ParamName);
    }

    private static WorkflowValidationRequest ValidRequest() =>
        new(
            new GitHubRepositoryFullName("ReleasedGroup", "TheConductor"),
            Profile(
                """
                tracker:
                  owner: ReleasedGroup
                  repo: TheConductor
                  api_key: $GITHUB_TOKEN
                agent:
                  max_turns: 20
                """),
            ExecutionMode.Docker,
            Port: 8080,
            WorkflowPath: "/config/WORKFLOW.md",
            DataPath: "/data",
            SecretReferences:
            [
                new WorkflowSecretReference(SecretType.GitHubToken, CredentialInheritanceMode.InheritDefault, isResolved: true),
                new WorkflowSecretReference(SecretType.OpenAiApiKey, CredentialInheritanceMode.SpecificSecret, SecretId.New()),
            ]);

    private static WorkflowProfile Profile(string source) =>
        new(WorkflowProfileId.New(), "Default", source, CreatedAtUtc);
}
