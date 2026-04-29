using Conductor.Core.Application.Instances;

namespace Conductor.Host.Endpoints;

public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapConductorInstances(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/instances")
            .WithTags("Instances");

        group.MapPost("/register", RegisterInstanceAsync)
            .WithName("RegisterSymphonyInstance")
            .Produces<ManualInstanceRegistrationResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> RegisterInstanceAsync(
        ManualInstanceRegistrationRequest request,
        IManualInstanceRegistrationService registrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            ManualInstanceRegistrationResult result =
                await registrationService.RegisterAsync(request, cancellationToken);

            return Results.Created($"/instances/{result.InstanceId}", result);
        }
        catch (ManualInstanceRegistrationValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors);
        }
        catch (DuplicateSymphonyInstanceRegistrationException ex)
        {
            return Results.Problem(
                title: "Symphony instance already registered",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["existingInstanceId"] = ex.ExistingInstanceId,
                    ["baseUrl"] = ex.BaseUrl.AbsoluteUri,
                });
        }
    }
}
