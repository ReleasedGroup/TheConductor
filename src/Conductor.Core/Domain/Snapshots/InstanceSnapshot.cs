using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Domain.Snapshots;

public sealed record InstanceSnapshot(
    SymphonyInstanceId SymphonyInstanceId,
    DateTimeOffset CapturedAtUtc,
    InstanceHealthStatus HealthStatus,
    string? HealthJson,
    string? RuntimeJson,
    string? StateJson);
