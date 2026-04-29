using Conductor.Host.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/favicon.ico", () => Results.NoContent()).ExcludeFromDescription();
app.MapGet("/health/live", () => Results.Ok(new HealthCheckResponse("Healthy", "Conductor.Host")));
app.MapGet("/health/ready", () => Results.Ok(new HealthCheckResponse("Ready", "Conductor.Host")));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal sealed record HealthCheckResponse(string Status, string Service);

public partial class Program;
