using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Snapshots;

namespace Conductor.Core.Application.Snapshots;

public interface IInstanceSnapshotStore
{
    Task AddAsync(InstanceSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<InstanceSnapshot?> GetLatestAsync(
        SymphonyInstanceId symphonyInstanceId,
        CancellationToken cancellationToken = default);
}
