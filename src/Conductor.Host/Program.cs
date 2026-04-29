using Conductor.Host.Components;
using Conductor.Host.Endpoints;
using Conductor.Host.Workers;
using Conductor.Infrastructure.Persistence.Sqlite;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHealthChecks();
builder.Services.AddConductorPersistence(builder.Configuration);
builder.Services.AddConductorWorkers();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue("Conductor:BootstrapDevelopmentDatabase", defaultValue: true))
{
    await app.Services.BootstrapDevelopmentDatabaseAsync(app.Logger);
}

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
