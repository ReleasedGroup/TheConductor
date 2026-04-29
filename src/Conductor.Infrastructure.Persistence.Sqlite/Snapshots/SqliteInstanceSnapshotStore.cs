using Conductor.Core.Application.Snapshots;
using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Snapshots;
using Conductor.Infrastructure.Persistence.Sqlite.Schema;
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

        dbContext.Set<InstanceSnapshotRecord>().Add(ToRecord(snapshot));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InstanceSnapshot?> GetLatestAsync(
        SymphonyInstanceId symphonyInstanceId,
        CancellationToken cancellationToken = default)
    {
        string instanceId = FormatId(symphonyInstanceId.Value);

        List<InstanceSnapshotRecord> records = await dbContext.Set<InstanceSnapshotRecord>()
            .AsNoTracking()
            .Where(snapshot => snapshot.SymphonyInstanceId == instanceId)
            .ToListAsync(cancellationToken);

        InstanceSnapshotRecord? record = records
            .OrderByDescending(snapshot => snapshot.CapturedAtUtc)
            .ThenByDescending(snapshot => snapshot.Id)
            .FirstOrDefault();

        return record is null ? null : ToDomain(record);
    }

    private static InstanceSnapshotRecord ToRecord(InstanceSnapshot snapshot) => new()
    {
        Id = FormatId(snapshot.Id.Value),
        SymphonyInstanceId = FormatId(snapshot.SymphonyInstanceId.Value),
        CapturedAtUtc = snapshot.CapturedAtUtc.ToUniversalTime(),
        HealthStatus = snapshot.HealthStatus.ToString(),
        HttpStatusCode = snapshot.HttpStatusCode,
        LatencyMilliseconds = snapshot.LatencyMilliseconds,
        ErrorMessage = snapshot.ErrorMessage,
        HealthJson = snapshot.HealthJson,
        RuntimeJson = snapshot.RuntimeJson,
        StateJson = snapshot.StateJson,
        ApplicationName = snapshot.ApplicationName,
        ApplicationVersion = snapshot.ApplicationVersion,
        RuntimeInstanceId = snapshot.RuntimeInstanceId,
        WorkflowOwner = snapshot.WorkflowOwner,
        WorkflowRepository = snapshot.WorkflowRepository,
        WorkflowSourcePath = snapshot.WorkflowSourcePath,
        PersistenceProvider = snapshot.PersistenceProvider,
        RuntimeDefaultsJson = snapshot.RuntimeDefaultsJson,
        ActiveIssueCount = snapshot.ActiveIssueCount,
        RunningSessionCount = snapshot.RunningSessionCount,
        RetryQueueCount = snapshot.RetryQueueCount,
        FailedRunCount = snapshot.FailedRunCount,
        TokenInputTotal = snapshot.TokenInputTotal,
        TokenOutputTotal = snapshot.TokenOutputTotal,
    };

    private static InstanceSnapshot ToDomain(InstanceSnapshotRecord record) => new(
        InstanceSnapshotId.Parse(record.Id),
        SymphonyInstanceId.Parse(record.SymphonyInstanceId),
        record.CapturedAtUtc.ToUniversalTime(),
        Enum.Parse<InstanceHealthStatus>(record.HealthStatus),
        record.HealthJson,
        record.RuntimeJson,
        record.StateJson,
        record.ActiveIssueCount,
        record.RunningSessionCount,
        record.RetryQueueCount,
        record.FailedRunCount,
        record.TokenInputTotal,
        record.TokenOutputTotal,
        record.HttpStatusCode,
        record.LatencyMilliseconds,
        record.ErrorMessage,
        record.ApplicationName,
        record.ApplicationVersion,
        record.RuntimeInstanceId,
        record.WorkflowOwner,
        record.WorkflowRepository,
        record.WorkflowSourcePath,
        record.PersistenceProvider,
        record.RuntimeDefaultsJson);

    private static string FormatId(Guid id) => id.ToString("D");
}
