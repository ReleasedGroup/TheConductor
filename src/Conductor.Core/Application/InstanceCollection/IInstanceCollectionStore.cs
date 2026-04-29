namespace Conductor.Core.Application.InstanceCollection;

public interface IInstanceCollectionStore
{
    Task<IReadOnlyList<CollectableSymphonyInstance>> ListCollectableInstancesAsync(
        CancellationToken cancellationToken);

    Task SaveCollectionResultAsync(
        InstanceCollectionResult result,
        CancellationToken cancellationToken);
}
