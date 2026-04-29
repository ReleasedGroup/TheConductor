using System.Text.Json.Serialization;
using Conductor.Core.Application.Dashboard;
using Conductor.Host.Components;
using Conductor.Host.Dashboard;
using Conductor.Host.Endpoints;
using Conductor.Host.Workers;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Secrets;
using Conductor.Infrastructure.Symphony;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<DashboardProjectionOptions>(
    builder.Configuration.GetSection(DashboardProjectionOptions.SectionName));
builder.Services.AddSingleton<IDashboardProjectionStore, JsonFileDashboardProjectionStore>();
builder.Services.AddHealthChecks();
builder.Services.AddConductorPersistence(builder.Configuration);
builder.Services.AddConductorSecrets();
builder.Services.AddConductorSymphony();
builder.Services.AddConductorWorkers(builder.Configuration);

WebApplication app = builder.Build();

await app.Services.ApplyConductorPersistenceMigrationsAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/favicon.ico", () => Results.NoContent()).ExcludeFromDescription();
app.MapConductorHealth();
app.MapConductorInstances();
app.MapConductorRepositories();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
