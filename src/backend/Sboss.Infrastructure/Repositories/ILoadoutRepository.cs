namespace Sboss.Infrastructure.Repositories;

public interface ILoadoutRepository
{
    Task<LoadoutSnapshot?> GetAsync(Guid accountId, Guid levelSeedId, CancellationToken cancellationToken);
    Task<LoadoutSnapshot> UpsertAsync(LoadoutSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed record LoadoutSnapshot(
    Guid AccountId,
    Guid LevelSeedId,
    int MaxCapacity,
    int UsedCapacity,
    bool IsComplete,
    IReadOnlyDictionary<string, int> QuantitiesByItemCode);
