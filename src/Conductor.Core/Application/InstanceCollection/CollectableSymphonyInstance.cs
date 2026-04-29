using Conductor.Core.Domain;
using Conductor.Core.Domain.Ids;

namespace Conductor.Core.Application.InstanceCollection;

public sealed record CollectableSymphonyInstance(
    SymphonyInstanceId Id,
    RepositoryId RepositoryId,
    string DisplayName,
    Uri BaseUrl,
    InstanceLifecycleStatus LifecycleStatus,
    InstanceHealthStatus HealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc,
    DateTimeOffset? LastSeenAtUtc);
