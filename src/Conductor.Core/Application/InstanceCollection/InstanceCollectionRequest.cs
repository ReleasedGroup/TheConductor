namespace Conductor.Core.Application.InstanceCollection;

public sealed record InstanceCollectionRequest(
    bool IncludeRuntime,
    bool IncludeState);
