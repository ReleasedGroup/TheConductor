using Bunit;
using Conductor.Core.Application.Queries;
using Conductor.Core.Application.Repositories;
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

        IRenderedComponent<Repositories> page = context.Render<Repositories>();
        page.Find("#repository-full-name").Change("ReleasedGroup/TheConductor");
        page.Find("#create-instance-shell").Change(true);
        page.Find("#instance-base-url").Change("http://localhost:8080/");

        await page.Find("form").SubmitAsync();

        Assert.NotNull(importService.LastRequest);
        Assert.Equal("ReleasedGroup/TheConductor", importService.LastRequest.RepositoryFullName);
        Assert.True(importService.LastRequest.CreateSymphonyInstance);
        Assert.Equal("http://localhost:8080/", importService.LastRequest.InstanceBaseUrl);
        Assert.Equal(CredentialInheritanceMode.InheritDefault, importService.LastRequest.GitHubCredentialInheritanceMode);
        Assert.Contains("ReleasedGroup/TheConductor imported", page.Markup, StringComparison.Ordinal);
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
                DateTimeOffset.Parse("2026-04-29T02:00:00Z")));
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
        public Task<IReadOnlyList<ProjectListItemProjection>> ListProjectsAsync(
            ProjectListQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProjectListItemProjection> projects =
            [
                new ProjectListItemProjection(
                    ProjectId.New(),
                    "Platform",
                    "ReleasedGroup",
                    ProjectStatus.Active),
            ];

            return Task.FromResult(projects);
        }
    }
}
