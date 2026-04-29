using Conductor.Core.Application.Instances;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

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

        group.MapPut("/{instanceId:guid}/credentials", AssignCredentialsAsync)
            .WithName("AssignSymphonyInstanceCredentials")
            .Produces<InstanceCredentialAssignmentResult>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

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

    private static async Task<IResult> AssignCredentialsAsync(
        Guid instanceId,
        AssignInstanceCredentialsRequest request,
        IInstanceCredentialAssignmentService assignmentService,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = ValidateAssignmentRequest(
            instanceId,
            request,
            out InstanceCredentialAssignmentRequest? assignmentRequest);

        if (errors.Count > 0 || assignmentRequest is null)
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            InstanceCredentialAssignmentResult result =
                await assignmentService.AssignAsync(assignmentRequest, cancellationToken);

            return Results.Ok(result);
        }
        catch (InstanceCredentialAssignmentValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors);
        }
        catch (SymphonyInstanceNotFoundException ex)
        {
            return Results.Problem(
                title: "Symphony instance not found",
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["instanceId"] = ex.InstanceId,
                });
        }
    }

    private static Dictionary<string, string[]> ValidateAssignmentRequest(
        Guid instanceId,
        AssignInstanceCredentialsRequest request,
        out InstanceCredentialAssignmentRequest? assignmentRequest)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
        assignmentRequest = null;

        if (instanceId == Guid.Empty)
        {
            errors["instanceId"] = ["A Symphony instance id is required."];
        }

        CredentialAssignmentSelection? gitHubCredential = ParseSelection(
            "gitHubCredential",
            request.GitHubCredential,
            errors);
        CredentialAssignmentSelection? openAiCredential = ParseSelection(
            "openAiCredential",
            request.OpenAiCredential,
            errors);

        if (errors.Count > 0 || gitHubCredential is null || openAiCredential is null)
        {
            return errors;
        }

        assignmentRequest = new InstanceCredentialAssignmentRequest(
            new SymphonyInstanceId(instanceId),
            gitHubCredential,
            openAiCredential,
            request.RequestedByUserId);

        return errors;
    }

    private static CredentialAssignmentSelection? ParseSelection(
        string fieldName,
        AssignCredentialSelectionRequest? request,
        Dictionary<string, string[]> errors)
    {
        if (request is null)
        {
            errors[fieldName] = ["Credential assignment is required."];
            return null;
        }

        if (!TryParseCredentialMode(request.InheritanceMode, out CredentialInheritanceMode mode))
        {
            errors[$"{fieldName}.inheritanceMode"] = ["Use InheritDefault, SpecificSecret, or None."];
            return null;
        }

        SecretId? secretId = null;
        if (!string.IsNullOrWhiteSpace(request.SecretId))
        {
            if (!SecretId.TryParse(request.SecretId, out SecretId parsedSecretId))
            {
                errors[$"{fieldName}.secretId"] = ["Secret id must be a non-empty GUID."];
                return null;
            }

            secretId = parsedSecretId;
        }

        return new CredentialAssignmentSelection(mode, secretId);
    }

    private static bool TryParseCredentialMode(
        string? value,
        out CredentialInheritanceMode mode)
    {
        mode = default;

        return !string.IsNullOrWhiteSpace(value) &&
            Enum.GetNames<CredentialInheritanceMode>()
                .Any(name => string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            Enum.TryParse(value.Trim(), ignoreCase: true, out mode);
    }

    public sealed record AssignInstanceCredentialsRequest(
        AssignCredentialSelectionRequest? GitHubCredential,
        AssignCredentialSelectionRequest? OpenAiCredential,
        string? RequestedByUserId = null);

    public sealed record AssignCredentialSelectionRequest(
        string? InheritanceMode,
        string? SecretId = null);
}
