using Bunit;
using Conductor.Core.Abstractions.Secrets;
using Conductor.Core.Domain;
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
        RecordingSecretStore store = new(
            new SecretDescriptor(
                SecretId.New(),
                "Production OpenAI key",
                SecretType.OpenAiApiKey,
                SecretScopeType.Global,
                scopeId: null,
                DateTimeOffset.Parse("2026-04-29T00:10:00Z")));
        store.SetRawValue("Production OpenAI key", "sk-existing-secret-value");
        context.Services.AddSingleton<ISecretStore>(store);

        IRenderedComponent<Secrets> page = context.Render<Secrets>();

        Assert.Contains("Secrets", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Production OpenAI key", page.Markup, StringComparison.Ordinal);
        Assert.Contains("OpenAI API key", page.Markup, StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", page.Markup, StringComparison.Ordinal);
        Assert.Contains(SecretTypeMetadata.MaskedDisplayValue, page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-existing-secret-value", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Secrets_Renders_Empty_State_When_No_Descriptors_Exist()
    {
        using BunitContext context = new();
        context.Services.AddSingleton<ISecretStore>(new RecordingSecretStore());

        IRenderedComponent<Secrets> page = context.Render<Secrets>();

        Assert.Contains("No secret descriptors yet.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("GitHub PAT", page.Markup, StringComparison.Ordinal);
        Assert.Contains("OpenAI API key", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Secrets_Creates_Rotates_And_Deletes_Without_Rendering_Values()
    {
        using BunitContext context = new();
        RecordingSecretStore store = new();
        context.Services.AddSingleton<ISecretStore>(store);

        IRenderedComponent<Secrets> page = context.Render<Secrets>();

        page.Find("input[name='secret-name']").Input("Release Portal PAT");
        page.Find("input[name='secret-value']").Input("ghp_created-secret-value");
        page.Find("form[data-secret-create]").Submit();

        page.WaitForAssertion(() =>
        {
            CreateSecretRequest createdRequest = Assert.IsType<CreateSecretRequest>(store.CreatedRequest);
            Assert.Equal("ghp_created-secret-value", createdRequest.Value);
            Assert.Contains("Release Portal PAT", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("ghp_created-secret-value", page.Markup, StringComparison.Ordinal);
        });

        SecretId createdId = store.CreatedSecretId;
        page.Find($"input[name='secret-rotate-{createdId}']").Input("ghp_rotated-secret-value");
        page.FindAll("button")
            .Single(button => button.TextContent.Contains("Rotate", StringComparison.Ordinal))
            .Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(createdId, store.RotatedSecretId);
            RotateSecretRequest rotatedRequest = Assert.IsType<RotateSecretRequest>(store.RotatedRequest);
            Assert.Equal("ghp_rotated-secret-value", rotatedRequest.Value);
            Assert.DoesNotContain("ghp_rotated-secret-value", page.Markup, StringComparison.Ordinal);
        });

        page.FindAll("button")
            .Single(button => button.TextContent.Contains("Delete", StringComparison.Ordinal))
            .Click();

        page.WaitForAssertion(() =>
        {
            Assert.Equal(createdId, store.DeletedSecretId);
            Assert.Contains("No secret descriptors yet.", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Release Portal PAT", page.Markup, StringComparison.Ordinal);
        });
    }

    private sealed class RecordingSecretStore : ISecretStore
    {
        private readonly List<SecretDescriptor> descriptors;
        private readonly Dictionary<SecretId, string> rawValues = [];

        public RecordingSecretStore(params SecretDescriptor[] descriptors)
        {
            this.descriptors = descriptors.ToList();
        }

        public CreateSecretRequest? CreatedRequest { get; private set; }

        public SecretId CreatedSecretId { get; private set; }

        public RotateSecretRequest? RotatedRequest { get; private set; }

        public SecretId RotatedSecretId { get; private set; }

        public SecretId DeletedSecretId { get; private set; }

        public Task<SecretDescriptor> CreateAsync(CreateSecretRequest request, CancellationToken cancellationToken)
        {
            CreatedRequest = request;
            CreatedSecretId = SecretId.New();
            var descriptor = new SecretDescriptor(
                CreatedSecretId,
                request.Name,
                request.SecretType,
                request.ScopeType,
                request.ScopeId,
                DateTimeOffset.Parse("2026-04-29T01:20:00Z"));

            descriptors.Add(descriptor);
            rawValues[descriptor.Id] = request.Value;

            return Task.FromResult(descriptor);
        }

        public Task RotateAsync(
            SecretId secretId,
            RotateSecretRequest request,
            CancellationToken cancellationToken)
        {
            RotatedSecretId = secretId;
            RotatedRequest = request;
            rawValues[secretId] = request.Value;
            descriptors.Single(descriptor => descriptor.Id == secretId)
                .MarkRotated(DateTimeOffset.Parse("2026-04-29T01:30:00Z"));

            return Task.CompletedTask;
        }

        public Task<ResolvedSecret> ResolveAsync(
            SecretReference reference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ResolvedSecret(reference.SecretId, rawValues[reference.SecretId]));
        }

        public Task<ResolvedSecret?> ResolveAsync(
            SecretResolutionRequest request,
            CancellationToken cancellationToken)
        {
            SecretDescriptor? descriptor = descriptors.FirstOrDefault(secret => secret.SecretType == request.SecretType);

            return Task.FromResult(descriptor is null
                ? null
                : new ResolvedSecret(descriptor.Id, rawValues[descriptor.Id]));
        }

        public Task<IReadOnlyList<SecretDescriptor>> ListAsync(
            SecretQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SecretDescriptor>>(
                descriptors
                    .Where(descriptor => query.SecretType is null || descriptor.SecretType == query.SecretType)
                    .Where(descriptor => query.ScopeType is null || descriptor.ScopeType == query.ScopeType)
                    .OrderBy(descriptor => descriptor.Name, StringComparer.Ordinal)
                    .ToList());
        }

        public Task DeleteAsync(SecretId secretId, CancellationToken cancellationToken)
        {
            DeletedSecretId = secretId;
            descriptors.RemoveAll(descriptor => descriptor.Id == secretId);
            rawValues.Remove(secretId);

            return Task.CompletedTask;
        }

        public void SetRawValue(string name, string value)
        {
            SecretDescriptor descriptor = descriptors.Single(secret => secret.Name == name);
            rawValues[descriptor.Id] = value;
        }
    }
}
