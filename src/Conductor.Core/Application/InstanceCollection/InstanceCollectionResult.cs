using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.InstanceCollection;

public sealed record InstanceCollectionResult(
    SymphonyInstanceId InstanceId,
    RepositoryId RepositoryId,
    DateTimeOffset CapturedAtUtc,
    InstanceHealthStatus HealthStatus,
    int? HttpStatusCode,
    long? LatencyMilliseconds,
    string? ErrorMessage,
    string? HealthJson,
    string? RuntimeJson,
    string? StateJson);
