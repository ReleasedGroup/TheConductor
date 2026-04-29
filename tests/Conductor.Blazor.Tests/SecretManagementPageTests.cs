using Bunit;
using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Application.Secrets;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Secrets;
using Conductor.Host.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Blazor.Tests;

public sealed class SecretManagementPageTests
{
    [Fact]
    public void Secrets_Renders_OpenAi_Key_Descriptor_With_Masked_Value()
    {
        using BunitContext context = new();
        DateTimeOffset createdAtUtc = DateTimeOffset.Parse("2026-04-29T00:10:00Z");
        SecretDescriptor descriptor = new(
            SecretId.New(),
            "Production OpenAI key",
            SecretType.OpenAiApiKey,
            SecretScopeType.Global,
            scopeId: null,
            createdAtUtc);

        context.Services.AddSingleton<ISecretDescriptorQueryService>(
            new StaticSecretDescriptorQueryService([SecretDescriptorView.FromDescriptor(descriptor)]));

        IRenderedComponent<Secrets> page = context.Render<Secrets>();

        Assert.Contains("Secrets", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Production OpenAI key", page.Markup, StringComparison.Ordinal);
        Assert.Contains("OpenAI API key", page.Markup, StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", page.Markup, StringComparison.Ordinal);
        Assert.Contains(SecretTypeMetadata.MaskedDisplayValue, page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Secrets_Renders_Empty_State_When_No_Descriptors_Exist()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<ISecretDescriptorQueryService>(
            new StaticSecretDescriptorQueryService([]));

        IRenderedComponent<Secrets> page = context.Render<Secrets>();

        Assert.Contains("No secret descriptors yet.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("GitHub PAT", page.Markup, StringComparison.Ordinal);
        Assert.Contains("OpenAI API key", page.Markup, StringComparison.Ordinal);
    }

    private sealed class StaticSecretDescriptorQueryService(IReadOnlyList<SecretDescriptorView> descriptors)
        : ISecretDescriptorQueryService
    {
        public Task<IReadOnlyList<SecretDescriptorView>> ListAsync(
            SecretQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(descriptors);
        }
    }
}
