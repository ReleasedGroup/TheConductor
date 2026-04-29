namespace Conductor.Host.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapConductorHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        return app;
    }
}
