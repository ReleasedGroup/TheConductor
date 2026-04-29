using Bunit;
using Conductor.Core.Application.Workflows;
using Conductor.Core.Domain.Ids;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class WorkflowProfilesPageTests
{
    [Fact]
    public async Task WorkflowProfiles_Creates_Profile_From_Editor_Form()
    {
        using BunitContext context = new();
        FakeWorkflowProfileService workflowProfileService = new();
        context.Services.AddSingleton<IWorkflowProfileService>(workflowProfileService);

        IRenderedComponent<WorkflowProfiles> page = context.Render<WorkflowProfiles>();
        page.Find("#workflow-profile-name").Change("Default Docker");
        page.Find("#workflow-profile-description").Change("Docker provisioning profile");
        page.Find("#workflow-profile-source").Change("# WORKFLOW\ntracker:\n  api_key: $GITHUB_TOKEN");
        page.Find("#workflow-profile-default").Change(true);

        await page.Find("form").SubmitAsync();

        Assert.NotNull(workflowProfileService.LastCreateRequest);
        Assert.Equal("Default Docker", workflowProfileService.LastCreateRequest.Name);
        Assert.Equal("Docker provisioning profile", workflowProfileService.LastCreateRequest.Description);
        Assert.Equal("# WORKFLOW\ntracker:\n  api_key: $GITHUB_TOKEN", workflowProfileService.LastCreateRequest.WorkflowSource);
        Assert.True(workflowProfileService.LastCreateRequest.IsDefault);
        Assert.Contains("Workflow profile saved", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Default Docker is revision 1.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Default Docker", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowProfiles_Edits_Selected_Profile()
    {
        using BunitContext context = new();
        FakeWorkflowProfileService workflowProfileService = new();
        workflowProfileService.SeedProfile(
            FakeWorkflowProfileService.ExistingProfileId,
            "Local",
            "Local runner profile",
            "# WORKFLOW local",
            isDefault: false,
            revision: 2);
        context.Services.AddSingleton<IWorkflowProfileService>(workflowProfileService);

        IRenderedComponent<WorkflowProfiles> page = context.Render<WorkflowProfiles>();
        page.Find("button.table-action").Click();
        page.Find("#workflow-profile-name").Change("Company Standard");
        page.Find("#workflow-profile-description").Change("Shared local runner profile");
        page.Find("#workflow-profile-source").Change("# WORKFLOW local\ntracker:\n  api_key: $GITHUB_TOKEN");
        page.Find("#workflow-profile-default").Change(true);

        await page.Find("form").SubmitAsync();

        Assert.Equal(FakeWorkflowProfileService.ExistingProfileId, workflowProfileService.LastUpdateProfileId);
        Assert.NotNull(workflowProfileService.LastUpdateRequest);
        Assert.Equal("Company Standard", workflowProfileService.LastUpdateRequest.Name);
        Assert.Equal("Shared local runner profile", workflowProfileService.LastUpdateRequest.Description);
        Assert.True(workflowProfileService.LastUpdateRequest.IsDefault);
        Assert.Contains("Company Standard is revision 3.", page.Markup, StringComparison.Ordinal);
    }

    private sealed class FakeWorkflowProfileService : IWorkflowProfileService
    {
        public static readonly WorkflowProfileId ExistingProfileId =
            WorkflowProfileId.Parse("44444444-4444-4444-4444-444444444444");

        private readonly Dictionary<WorkflowProfileId, WorkflowProfileDetail> details = [];

        public WorkflowProfileMutationRequest? LastCreateRequest { get; private set; }

        public WorkflowProfileMutationRequest? LastUpdateRequest { get; private set; }

        public WorkflowProfileId? LastUpdateProfileId { get; private set; }

        public Task<IReadOnlyList<WorkflowProfileSummary>> ListAsync(
            WorkflowProfileQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkflowProfileSummary> profiles = details.Values
                .OrderByDescending(profile => profile.IsDefault)
                .ThenBy(profile => profile.Name)
                .Select(profile => new WorkflowProfileSummary(
                    profile.Id,
                    profile.Name,
                    profile.Description,
                    profile.IsDefault,
                    profile.Revision,
                    profile.CreatedAtUtc,
                    profile.UpdatedAtUtc))
                .ToArray();

            return Task.FromResult(profiles);
        }

        public Task<WorkflowProfileDetail?> GetAsync(
            WorkflowProfileId profileId,
            CancellationToken cancellationToken = default)
        {
            details.TryGetValue(profileId, out WorkflowProfileDetail? profile);

            return Task.FromResult(profile);
        }

        public Task<WorkflowProfileMutationResult> CreateAsync(
            WorkflowProfileMutationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            WorkflowProfileId profileId = WorkflowProfileId.New();
            DateTimeOffset now = DateTimeOffset.Parse("2026-04-29T06:00:00Z");

            details[profileId] = new WorkflowProfileDetail(
                profileId,
                request.Name,
                request.Description,
                request.WorkflowSource,
                request.IsDefault,
                Revision: 1,
                now,
                now);

            return Task.FromResult(new WorkflowProfileMutationResult(
                profileId,
                request.Name,
                request.IsDefault,
                Revision: 1,
                now));
        }

        public Task<WorkflowProfileMutationResult> UpdateAsync(
            WorkflowProfileId profileId,
            WorkflowProfileMutationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastUpdateProfileId = profileId;
            LastUpdateRequest = request;
            WorkflowProfileDetail existing = details[profileId];
            DateTimeOffset updatedAt = existing.UpdatedAtUtc.AddMinutes(15);
            int revision = existing.Revision + 1;

            details[profileId] = existing with
            {
                Name = request.Name,
                Description = request.Description,
                WorkflowSource = request.WorkflowSource,
                IsDefault = request.IsDefault,
                Revision = revision,
                UpdatedAtUtc = updatedAt,
            };

            return Task.FromResult(new WorkflowProfileMutationResult(
                profileId,
                request.Name,
                request.IsDefault,
                revision,
                updatedAt));
        }

        public void SeedProfile(
            WorkflowProfileId profileId,
            string name,
            string? description,
            string workflowSource,
            bool isDefault,
            int revision)
        {
            DateTimeOffset createdAt = DateTimeOffset.Parse("2026-04-29T06:00:00Z");
            details[profileId] = new WorkflowProfileDetail(
                profileId,
                name,
                description,
                workflowSource,
                isDefault,
                revision,
                createdAt,
                createdAt);
        }
    }
}
