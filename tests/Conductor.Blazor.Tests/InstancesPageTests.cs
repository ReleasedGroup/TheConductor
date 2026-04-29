using Bunit;
using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Application.Instances;
using Conductor.Core.Application.Queries;
using Conductor.Core.Application.Secrets;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class InstancesPageTests
{
    [Fact]
    public async Task Instances_Submits_Manual_Registration_Request()
    {
        using BunitContext context = new();
        FakeManualInstanceRegistrationService registrationService = new();
        FakeInstanceCredentialAssignmentService credentialAssignmentService = new();
        context.Services.AddSingleton<IManualInstanceRegistrationService>(registrationService);
        context.Services.AddSingleton<IInstanceCredentialAssignmentService>(credentialAssignmentService);
        context.Services.AddSingleton<IInstanceSummaryQueryService>(new StaticInstanceSummaryQueryService());
        context.Services.AddSingleton<ISecretDescriptorQueryService>(new StaticSecretDescriptorQueryService());

        IRenderedComponent<Instances> page = context.Render<Instances>();
        page.Find("input[type='url']").Change("http://localhost:5173");
        page.Find("input[type='text']").Change("Billing Symphony");

        await page.Find("form").SubmitAsync();

        Assert.NotNull(registrationService.LastRequest);
        Assert.Equal("http://localhost:5173", registrationService.LastRequest.BaseUrl);
        Assert.Equal("Billing Symphony", registrationService.LastRequest.DisplayName);
        Assert.Contains("Billing Symphony registered", page.Markup, StringComparison.Ordinal);
        Assert.Contains("ReleasedGroup/BillingApi is reporting Healthy health.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Registered instances", page.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"http://localhost:5173/\"", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Instances_Submits_Credential_Assignment_Request()
    {
        using BunitContext context = new();
        FakeInstanceCredentialAssignmentService credentialAssignmentService = new();
        context.Services.AddSingleton<IManualInstanceRegistrationService>(new FakeManualInstanceRegistrationService());
        context.Services.AddSingleton<IInstanceCredentialAssignmentService>(credentialAssignmentService);
        context.Services.AddSingleton<IInstanceSummaryQueryService>(new StaticInstanceSummaryQueryService(
            CredentialInheritanceMode.SpecificSecret,
            StaticSecretDescriptorQueryService.GitHubSecretId,
            CredentialInheritanceMode.SpecificSecret,
            StaticSecretDescriptorQueryService.OpenAiSecretId));
        context.Services.AddSingleton<ISecretDescriptorQueryService>(new StaticSecretDescriptorQueryService());

        IRenderedComponent<Instances> page = context.Render<Instances>();

        await page.FindAll("form")[1].SubmitAsync();

        Assert.NotNull(credentialAssignmentService.LastRequest);
        Assert.Equal(StaticInstanceSummaryQueryService.InstanceId, credentialAssignmentService.LastRequest.InstanceId);
        Assert.Equal(StaticSecretDescriptorQueryService.GitHubSecretId, credentialAssignmentService.LastRequest.GitHubCredential.SecretId);
        Assert.Equal(StaticSecretDescriptorQueryService.OpenAiSecretId, credentialAssignmentService.LastRequest.OpenAiCredential.SecretId);
        Assert.Contains("Credentials updated", page.Markup, StringComparison.Ordinal);
    }

    private sealed class FakeManualInstanceRegistrationService : IManualInstanceRegistrationService
    {
        public ManualInstanceRegistrationRequest? LastRequest { get; private set; }

        public Task<ManualInstanceRegistrationResult> RegisterAsync(
            ManualInstanceRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(new ManualInstanceRegistrationResult(
                Guid.NewGuid().ToString("D"),
                Guid.NewGuid().ToString("D"),
                "ReleasedGroup/BillingApi",
                "Billing Symphony",
                new Uri("http://localhost:5173/"),
                "Running",
                "Healthy",
                DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                "3.1.4",
                Guid.NewGuid().ToString("D")));
        }
    }

    private sealed class FakeInstanceCredentialAssignmentService : IInstanceCredentialAssignmentService
    {
        public InstanceCredentialAssignmentRequest? LastRequest { get; private set; }

        public Task<InstanceCredentialAssignmentResult> AssignAsync(
            InstanceCredentialAssignmentRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(new InstanceCredentialAssignmentResult(
                request.InstanceId.ToString(),
                new CredentialAssignmentSummary(
                    request.GitHubCredential.InheritanceMode.ToString(),
                    request.GitHubCredential.SecretId?.ToString(),
                    "Repository GitHub token",
                    SecretType.GitHubToken.ToString(),
                    SecretScopeType.Repository.ToString(),
                    StaticInstanceSummaryQueryService.RepositoryId.ToString()),
                new CredentialAssignmentSummary(
                    request.OpenAiCredential.InheritanceMode.ToString(),
                    request.OpenAiCredential.SecretId?.ToString(),
                    "Instance OpenAI key",
                    SecretType.OpenAiApiKey.ToString(),
                    SecretScopeType.SymphonyInstance.ToString(),
                    request.InstanceId.ToString()),
                DateTimeOffset.Parse("2026-04-29T02:00:00Z")));
        }
    }

    private sealed class StaticInstanceSummaryQueryService : IInstanceSummaryQueryService
    {
        public static readonly SymphonyInstanceId InstanceId = SymphonyInstanceId.New();
        public static readonly RepositoryId RepositoryId = RepositoryId.New();
        private readonly CredentialInheritanceMode gitHubMode;
        private readonly SecretId? gitHubSecretId;
        private readonly CredentialInheritanceMode openAiMode;
        private readonly SecretId? openAiSecretId;

        public StaticInstanceSummaryQueryService(
            CredentialInheritanceMode gitHubMode = CredentialInheritanceMode.InheritDefault,
            SecretId? gitHubSecretId = null,
            CredentialInheritanceMode openAiMode = CredentialInheritanceMode.InheritDefault,
            SecretId? openAiSecretId = null)
        {
            this.gitHubMode = gitHubMode;
            this.gitHubSecretId = gitHubSecretId;
            this.openAiMode = openAiMode;
            this.openAiSecretId = openAiSecretId;
        }

        public Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
            InstanceSummaryQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InstanceSummaryProjection> summaries =
            [
                new InstanceSummaryProjection(
                    InstanceId,
                    RepositoryId,
                    "ReleasedGroup/BillingApi",
                    null,
                    null,
                    "Billing Symphony",
                    ExecutionMode.Docker,
                    new Uri("http://localhost:5173/"),
                    InstanceLifecycleStatus.Running,
                    InstanceHealthStatus.Healthy,
                    DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                    DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                    DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                    GitHubCredentialInheritanceMode: gitHubMode,
                    GitHubCredentialSecretId: gitHubSecretId,
                    GitHubCredentialName: gitHubSecretId is null ? null : "Repository GitHub token",
                    OpenAiCredentialInheritanceMode: openAiMode,
                    OpenAiCredentialSecretId: openAiSecretId,
                    OpenAiCredentialName: openAiSecretId is null ? null : "Instance OpenAI key"),
            ];

            return Task.FromResult(summaries);
        }
    }

    private sealed class StaticSecretDescriptorQueryService : ISecretDescriptorQueryService
    {
        public static readonly SecretId GitHubSecretId = SecretId.New();
        public static readonly SecretId OpenAiSecretId = SecretId.New();

        public Task<IReadOnlyList<SecretDescriptorView>> ListAsync(
            SecretQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SecretDescriptorView> descriptors =
            [
                SecretDescriptorView.FromDescriptor(new SecretDescriptor(
                    GitHubSecretId,
                    "Repository GitHub token",
                    SecretType.GitHubToken,
                    SecretScopeType.Repository,
                    StaticInstanceSummaryQueryService.RepositoryId.ToString(),
                    DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
                    null)),
                SecretDescriptorView.FromDescriptor(new SecretDescriptor(
                    OpenAiSecretId,
                    "Instance OpenAI key",
                    SecretType.OpenAiApiKey,
                    SecretScopeType.SymphonyInstance,
                    StaticInstanceSummaryQueryService.InstanceId.ToString(),
                    DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
                    null)),
            ];

            return Task.FromResult(descriptors);
        }
    }
}
