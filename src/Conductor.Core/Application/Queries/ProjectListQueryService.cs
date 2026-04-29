using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.Queries;

public interface IProjectListQueryService
{
    Task<IReadOnlyList<ProjectListItemProjection>> ListProjectsAsync(
        ProjectListQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectListQuery(
    bool IncludeArchived = false,
    string? Search = null);

public sealed record ProjectListItemProjection(
    ProjectId Id,
    string Name,
    string OwnerName,
    ProjectStatus Status);
