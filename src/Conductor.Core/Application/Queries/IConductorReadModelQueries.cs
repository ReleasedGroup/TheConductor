using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.Queries;

public interface IConductorReadModelQueries
{
    Task<ConductorDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositoryOverview>> ListRepositoriesAsync(CancellationToken cancellationToken = default);

    Task<RepositoryOverview?> GetRepositoryAsync(RepositoryId repositoryId, CancellationToken cancellationToken = default);
}
