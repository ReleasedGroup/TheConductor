namespace Conductor.Core.Application.Dashboard;

public interface IDashboardProjectionStore
{
    Task<DashboardProjection> GetCurrentAsync(CancellationToken cancellationToken = default);
}
