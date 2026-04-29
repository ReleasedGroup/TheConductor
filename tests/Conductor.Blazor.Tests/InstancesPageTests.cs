using Bunit;
using Conductor.Core.Application.Instances;
using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
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
        context.Services.AddSingleton<IManualInstanceRegistrationService>(registrationService);
        context.Services.AddSingleton<IInstanceSummaryQueryService>(new StaticInstanceSummaryQueryService());

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

    private sealed class StaticInstanceSummaryQueryService : IInstanceSummaryQueryService
    {
        public Task<IReadOnlyList<InstanceSummaryProjection>> ListInstanceSummariesAsync(
            InstanceSummaryQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InstanceSummaryProjection> summaries =
            [
                new InstanceSummaryProjection(
                    SymphonyInstanceId.New(),
                    RepositoryId.New(),
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
                    DateTimeOffset.Parse("2026-04-29T02:00:00Z")),
            ];

            return Task.FromResult(summaries);
        }
    }
}
