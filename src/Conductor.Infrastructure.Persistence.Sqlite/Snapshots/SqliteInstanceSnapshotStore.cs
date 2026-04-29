using Conductor.Core.Application.Snapshots;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite.Snapshots;

public sealed class SqliteInstanceSnapshotStore : IInstanceSnapshotStore
{
    private readonly ConductorDbContext dbContext;

    public SqliteInstanceSnapshotStore(ConductorDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task AddAsync(InstanceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        dbContext.InstanceSnapshots.Add(snapshot);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InstanceSnapshot?> GetLatestAsync(
        SymphonyInstanceId symphonyInstanceId,
        CancellationToken cancellationToken = default)
    {
        List<InstanceSnapshot> snapshots = await dbContext.InstanceSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.SymphonyInstanceId == symphonyInstanceId)
            .ToListAsync(cancellationToken);

        return snapshots
            .OrderByDescending(snapshot => snapshot.CapturedAtUtc)
            .ThenByDescending(snapshot => snapshot.Id.Value)
            .FirstOrDefault();
    }
}
