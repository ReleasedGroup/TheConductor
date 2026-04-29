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
        public Task<IReadOnlyList<RepositoryListItemProjection>> ListRepositoriesAsync(
            RepositoryListQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RepositoryListItemProjection> repositories =
            [
                new RepositoryListItemProjection(
                    RepositoryId.New(),
                    null,
                    null,
                    RepositoryProvider.GitHub,
                    "ReleasedGroup",
                    "ExistingService",
                    "ReleasedGroup/ExistingService",
                    "main",
                    new Uri("https://github.com/ReleasedGroup/ExistingService"),
                    IsArchived: false,
                    InstanceCount: 1,
                    RunningInstanceCount: 0,
                    InstanceHealthStatus.Unknown,
                    LastHealthCheckAtUtc: null),
            ];

            return Task.FromResult(repositories);
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
