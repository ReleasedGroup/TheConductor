using System.Globalization;
using System.Text.Json;
using Conductor.Core.Application.Workflows;
using Conductor.Core.Domain.Auditing;
using Conductor.Core.Domain.Workflows;
using Conductor.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Persistence.Tests;

public sealed class SqliteWorkflowProfileServiceTests
{
    [Fact]
    public async Task CreateAsync_Persists_Profile_Metadata_And_Audit_Event()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-04-29T05:00:00Z");
        ManualTimeProvider timeProvider = new(now);
        await using ServiceProvider provider = BuildProvider(timeProvider);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        IWorkflowProfileService workflowProfiles =
            scope.ServiceProvider.GetRequiredService<IWorkflowProfileService>();

        WorkflowProfileMutationResult result = await workflowProfiles.CreateAsync(new WorkflowProfileMutationRequest(
            "  Default Docker  ",
            "  Docker provisioning profile  ",
            "  # WORKFLOW\ntracker:\n  api_key: $GITHUB_TOKEN  ",
            IsDefault: true,
            RequestedByUserId: "nick"));

        dbContext.ChangeTracker.Clear();
        WorkflowProfile profile = await dbContext.WorkflowProfiles.SingleAsync();
        AuditEvent auditEvent = await dbContext.AuditEvents.SingleAsync();
        WorkflowProfileDetail detail = (await workflowProfiles.GetAsync(result.Id))!;
        IReadOnlyList<WorkflowProfileSummary> summaries = await workflowProfiles.ListAsync(new WorkflowProfileQuery());

        Assert.Equal(profile.Id, result.Id);
        Assert.Equal("Default Docker", profile.Name);
        Assert.Equal("Docker provisioning profile", profile.Description);
        Assert.Equal("# WORKFLOW\ntracker:\n  api_key: $GITHUB_TOKEN", profile.WorkflowSource);
        Assert.True(profile.IsDefault);
        Assert.Equal(1, profile.Revision);
        Assert.Equal(now, profile.CreatedAtUtc);
        Assert.Equal(now, profile.UpdatedAtUtc);
        Assert.Equal(profile.WorkflowSource, detail.WorkflowSource);
        Assert.Single(summaries);
        Assert.Equal("CreateWorkflowProfile", auditEvent.Action);
        Assert.Equal("WorkflowProfile", auditEvent.TargetResourceType);
        Assert.Equal(profile.Id.ToString(), auditEvent.TargetResourceId);
        Assert.Equal("nick", auditEvent.ActorUserId);

        using JsonDocument metadata = JsonDocument.Parse(auditEvent.MetadataJson!);
        Assert.Equal(profile.Id.ToString(), metadata.RootElement.GetProperty("workflowProfileId").GetString());
        Assert.True(metadata.RootElement.GetProperty("IsDefault").GetBoolean());
        Assert.Equal(1, metadata.RootElement.GetProperty("Revision").GetInt32());
    }

    [Fact]
    public async Task UpdateAsync_Advances_Revision_And_Moves_Default_Flag()
    {
        DateTimeOffset createdAt = DateTimeOffset.Parse("2026-04-29T05:00:00Z");
        ManualTimeProvider timeProvider = new(createdAt);
        await using ServiceProvider provider = BuildProvider(timeProvider);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        IWorkflowProfileService workflowProfiles =
            scope.ServiceProvider.GetRequiredService<IWorkflowProfileService>();
        WorkflowProfileMutationResult first = await workflowProfiles.CreateAsync(new WorkflowProfileMutationRequest(
            "Docker",
            "Docker profile",
            "# WORKFLOW docker",
            IsDefault: true));
        WorkflowProfileMutationResult second = await workflowProfiles.CreateAsync(new WorkflowProfileMutationRequest(
            "Local",
            "Local profile",
            "# WORKFLOW local"));

        DateTimeOffset updatedAt = createdAt.AddMinutes(20);
        timeProvider.UtcNow = updatedAt;
        WorkflowProfileMutationResult updated = await workflowProfiles.UpdateAsync(
            second.Id,
            new WorkflowProfileMutationRequest(
                "Local runner",
                "Local process profile",
                "# WORKFLOW local\ntracker:\n  api_key: $GITHUB_TOKEN",
                IsDefault: true));

        dbContext.ChangeTracker.Clear();
        WorkflowProfile firstProfile = await dbContext.WorkflowProfiles.SingleAsync(profile => profile.Id == first.Id);
        WorkflowProfile secondProfile = await dbContext.WorkflowProfiles.SingleAsync(profile => profile.Id == second.Id);
        IReadOnlyList<WorkflowProfileSummary> summaries = await workflowProfiles.ListAsync(new WorkflowProfileQuery());

        Assert.False(firstProfile.IsDefault);
        Assert.Equal(2, firstProfile.Revision);
        Assert.Equal(updatedAt, firstProfile.UpdatedAtUtc);
        Assert.True(secondProfile.IsDefault);
        Assert.Equal("Local runner", secondProfile.Name);
        Assert.Equal(2, secondProfile.Revision);
        Assert.Equal(updatedAt, secondProfile.UpdatedAtUtc);
        Assert.Equal(2, updated.Revision);
        Assert.Equal(second.Id, summaries[0].Id);
        Assert.Equal(3, await dbContext.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_Rejects_Duplicate_Profile_Names()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-04-29T05:00:00Z");
        ManualTimeProvider timeProvider = new(now);
        await using ServiceProvider provider = BuildProvider(timeProvider);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ConductorDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConductorDbContext>();
        await dbContext.Database.MigrateAsync();
        IWorkflowProfileService workflowProfiles =
            scope.ServiceProvider.GetRequiredService<IWorkflowProfileService>();

        await workflowProfiles.CreateAsync(new WorkflowProfileMutationRequest(
            "Default",
            null,
            "# WORKFLOW"));

        WorkflowProfileValidationException exception =
            await Assert.ThrowsAsync<WorkflowProfileValidationException>(() =>
                workflowProfiles.CreateAsync(new WorkflowProfileMutationRequest(
                    " default ",
                    null,
                    "# WORKFLOW duplicate")));

        Assert.Contains(nameof(WorkflowProfileMutationRequest.Name), exception.Errors.Keys);
        Assert.Equal(1, await dbContext.WorkflowProfiles.CountAsync());
    }

    private static ServiceProvider BuildProvider(TimeProvider timeProvider)
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            "conductor-workflow-profile-tests",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "conductor.db");
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SqlitePersistenceOptions.ConnectionStringName}"] = $"Data Source={databasePath};Cache=Shared",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddConductorPersistence(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
