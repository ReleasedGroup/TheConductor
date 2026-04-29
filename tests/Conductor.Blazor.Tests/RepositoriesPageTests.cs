using Bunit;
using Conductor.Core.Application.Queries;
using Conductor.Core.Application.Repositories;
using Conductor.Core.Application.Workflows;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class RepositoriesPageTests
{
    [Fact]
    public async Task Repositories_Submits_Import_Request_With_Instance_Shell()
    {
        using BunitContext context = new();
        FakeRepositoryImportService importService = new();
        context.Services.AddSingleton<IRepositoryImportService>(importService);
        context.Services.AddSingleton<IRepositoryListQueryService>(new StaticRepositoryListQueryService());
        context.Services.AddSingleton<IProjectListQueryService>(new StaticProjectListQueryService());
        context.Services.AddSingleton<IWorkflowProfileService>(new StaticWorkflowProfileService());

        IRenderedComponent<Repositories> page = context.Render<Repositories>();
        page.Find("#repository-full-name").Change("ReleasedGroup/TheConductor");
        page.Find("#project-id").Change(StaticProjectListQueryService.PlatformProjectId.ToString());
        page.Find("#create-instance-shell").Change(true);
        page.Find("#instance-base-url").Change("http://localhost:8080/");
        page.Find("#workflow-profile-id").Change(StaticWorkflowProfileService.DefaultWorkflowProfileId.ToString());

        await page.Find("form").SubmitAsync();

        Assert.NotNull(importService.LastRequest);
        Assert.Equal("ReleasedGroup/TheConductor", importService.LastRequest.RepositoryFullName);
        Assert.Equal(StaticProjectListQueryService.PlatformProjectId, importService.LastRequest.ProjectId);
        Assert.True(importService.LastRequest.CreateSymphonyInstance);
        Assert.Equal("http://localhost:8080/", importService.LastRequest.InstanceBaseUrl);
        Assert.Equal(StaticWorkflowProfileService.DefaultWorkflowProfileId, importService.LastRequest.WorkflowProfileId);
        Assert.Equal(CredentialInheritanceMode.InheritDefault, importService.LastRequest.GitHubCredentialInheritanceMode);
        Assert.Contains("ReleasedGroup/TheConductor imported", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Linked to Platform.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Conductor local was created in NotProvisioned state.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Managed repositories", page.Markup, StringComparison.Ordinal);
        Assert.Contains("ReleasedGroup/ExistingService", page.Markup, StringComparison.Ordinal);
        Assert.Contains("main branch", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Eligible", page.Markup, StringComparison.Ordinal);
        Assert.Contains($"/repositories/{StaticRepositoryListQueryService.ExistingRepositoryId}", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDetail_Shows_Metadata_And_Attached_Instances()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<IRepositoryListQueryService>(new StaticRepositoryListQueryService());
        context.Services.AddSingleton<IInstanceSummaryQueryService>(new StaticInstanceSummaryQueryService());

        IRenderedComponent<RepositoryDetail> page = context.Render<RepositoryDetail>(parameters => parameters
            .Add(component => component.RepositoryId, StaticRepositoryListQueryService.ExistingRepositoryId.Value));

        Assert.Contains("Imported repository", page.Markup, StringComparison.Ordinal);
        Assert.Contains("ReleasedGroup/ExistingService", page.Markup, StringComparison.Ordinal);
        Assert.Contains("https://github.com/ReleasedGroup/ExistingService.git", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Private", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Existing local", page.Markup, StringComparison.Ordinal);
        Assert.Contains("3.1.4", page.Markup, StringComparison.Ordinal);
    }

    private sealed class FakeRepositoryImportService : IRepositoryImportService
    {
        public RepositoryImportRequest? LastRequest { get; private set; }

        public Task<RepositoryImportResult> ImportAsync(
            RepositoryImportRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(new RepositoryImportResult(
                Guid.NewGuid().ToString("D"),
                "ReleasedGroup/TheConductor",
                CreatedRepository: true,
                Guid.NewGuid().ToString("D"),
                CreatedSymphonyInstance: true,
                "Conductor local",
                DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                request.ProjectId?.ToString(),
                request.ProjectId is null ? null : "Platform"));
        }
    }

    private sealed class StaticRepositoryListQueryService : IRepositoryListQueryService
    {
        public static RepositoryId ExistingRepositoryId { get; } =
            RepositoryId.Parse("11111111-1111-1111-1111-111111111111");

        public Task<IReadOnlyList<RepositoryListItemProjection>> ListRepositoriesAsync(
            RepositoryListQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RepositoryListItemProjection> repositories =
            [
                new RepositoryListItemProjection(
                    ExistingRepositoryId,
                    null,
                    null,
                    RepositoryProvider.GitHub,
                    "ReleasedGroup",
                    "ExistingService",
                    "ReleasedGroup/ExistingService",
                    "main",
                    new Uri("https://github.com/ReleasedGroup/ExistingService.git"),
                    new Uri("https://github.com/ReleasedGroup/ExistingService"),
                    RepositoryVisibility.Private,
                    IsArchived: false,
                    LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                    OrchestrationStatus: RepositoryOrchestrationStatus.Eligible,
                    OrchestrationStatusReason: null,
                    InstanceCount: 1,
                    RunningInstanceCount: 0,
                    WorstHealthStatus: InstanceHealthStatus.Unknown,
                    LastHealthCheckAtUtc: null),
            ];

            return Task.FromResult(repositories);
        }

        public Task<RepositoryDetailProjection?> GetRepositoryAsync(
            RepositoryId repositoryId,
            CancellationToken cancellationToken = default)
        {
            if (repositoryId != ExistingRepositoryId)
            {
                return Task.FromResult<RepositoryDetailProjection?>(null);
            }

            return Task.FromResult<RepositoryDetailProjection?>(new RepositoryDetailProjection(
                ExistingRepositoryId,
                null,
                null,
                RepositoryProvider.GitHub,
                "ReleasedGroup",
                "ExistingService",
                "ReleasedGroup/ExistingService",
                "main",
                new Uri("https://github.com/ReleasedGroup/ExistingService.git"),
                new Uri("https://github.com/ReleasedGroup/ExistingService"),
                RepositoryVisibility.Private,
                IsArchived: false,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                OrchestrationStatus: RepositoryOrchestrationStatus.Eligible,
                OrchestrationStatusReason: null,
                InstanceCount: 1,
                RunningInstanceCount: 1,
                WorstHealthStatus: InstanceHealthStatus.Healthy,
                LastHealthCheckAtUtc: DateTimeOffset.Parse("2026-04-29T02:10:00Z")));
        }
    }

    private sealed class StaticInstanceSummaryQueryService : IInstanceSummaryQueryService
    {
        public Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
            InstanceSummaryQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InstanceSummaryProjection> summaries =
            [
                new InstanceSummaryProjection(
                    SymphonyInstanceId.Parse("22222222-2222-2222-2222-222222222222"),
                    StaticRepositoryListQueryService.ExistingRepositoryId,
                    "ReleasedGroup/ExistingService",
                    null,
                    null,
                    "Existing local",
                    ExecutionMode.LocalProcess,
                    new Uri("http://localhost:8080/"),
                    InstanceLifecycleStatus.Running,
                    InstanceHealthStatus.Healthy,
                    DateTimeOffset.Parse("2026-04-29T02:10:00Z"),
                    DateTimeOffset.Parse("2026-04-29T02:10:00Z"),
                    DateTimeOffset.Parse("2026-04-29T02:10:00Z"),
                    SymphonyVersion: "3.1.4",
                    SymphonyReleaseTag: "latest"),
            ];

            return Task.FromResult(summaries);
        }
    }

    private sealed class StaticProjectListQueryService : IProjectListQueryService
    {
        public static readonly ProjectId PlatformProjectId =
            ProjectId.Parse("bdfe0e41-15b5-4d7d-9153-7dd0f6fe1032");

        public Task<IReadOnlyList<ProjectListItemProjection>> ListProjectsAsync(
            ProjectListQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProjectListItemProjection> projects =
            [
                new ProjectListItemProjection(
                    PlatformProjectId,
                    "Platform",
                    "ReleasedGroup",
                    ProjectStatus.Active),
            ];

            return Task.FromResult(projects);
        }
    }

    private sealed class StaticWorkflowProfileService : IWorkflowProfileService
    {
        public static readonly WorkflowProfileId DefaultWorkflowProfileId =
            WorkflowProfileId.Parse("33333333-3333-3333-3333-333333333333");

        public Task<IReadOnlyList<WorkflowProfileSummary>> ListAsync(
            WorkflowProfileQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkflowProfileSummary> profiles =
            [
                new WorkflowProfileSummary(
                    DefaultWorkflowProfileId,
                    "Default Docker",
                    "Docker profile",
                    IsDefault: true,
                    Revision: 1,
                    CreatedAtUtc: DateTimeOffset.Parse("2026-04-29T02:00:00Z"),
                    UpdatedAtUtc: DateTimeOffset.Parse("2026-04-29T02:00:00Z")),
            ];

            return Task.FromResult(profiles);
        }

        public Task<WorkflowProfileDetail?> GetAsync(
            WorkflowProfileId profileId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WorkflowProfileMutationResult> CreateAsync(
            WorkflowProfileMutationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WorkflowProfileMutationResult> UpdateAsync(
            WorkflowProfileId profileId,
            WorkflowProfileMutationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
