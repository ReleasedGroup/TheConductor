using Conductor.Core.Application.Queries;
using Conductor.Core.Application.Repositories;

namespace Conductor.Host.Endpoints;

public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapConductorRepositories(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/repos")
            .WithTags("Repositories");

        group.MapGet("/", ListRepositoriesAsync)
            .WithName("ListRepositories")
            .Produces<IReadOnlyList<RepositoryListItemProjection>>();

        group.MapPost("/import", ImportRepositoryAsync)
            .WithName("ImportRepository")
            .Produces<RepositoryImportResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IReadOnlyList<RepositoryListItemProjection>> ListRepositoriesAsync(
        IRepositoryListQueryService repositoryListQueryService,
        CancellationToken cancellationToken)
    {
        return await repositoryListQueryService.ListRepositoriesAsync(
            new RepositoryListQuery(),
            cancellationToken);
    }

    private static async Task<IResult> ImportRepositoryAsync(
        RepositoryImportRequest request,
        IRepositoryImportService repositoryImportService,
        CancellationToken cancellationToken)
    {
        try
        {
            RepositoryImportResult result =
                await repositoryImportService.ImportAsync(request, cancellationToken);

            return Results.Created($"/repositories/{result.RepositoryId}", result);
        }
        catch (RepositoryImportValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors);
        }
    }
}
