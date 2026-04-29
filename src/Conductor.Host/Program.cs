using Conductor.Host.Components;
using Conductor.Host.Dashboard;
using Conductor.Host.Endpoints;
using Conductor.Host.Workers;
using Conductor.Infrastructure.Persistence.Sqlite;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<DashboardProjectionOptions>(
    builder.Configuration.GetSection(DashboardProjectionOptions.SectionName));
builder.Services.AddScoped<IDashboardProjectionStore, FileDashboardProjectionStore>();
builder.Services.AddHealthChecks();
builder.Services.AddConductorPersistence(builder.Configuration);
builder.Services.AddConductorWorkers();

WebApplication app = builder.Build();

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
