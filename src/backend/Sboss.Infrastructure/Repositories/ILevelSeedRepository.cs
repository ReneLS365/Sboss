using Sboss.Contracts.LevelSeeds;

namespace Sboss.Infrastructure.Repositories;

public interface ILevelSeedRepository
{
    Task<LevelSeedResponse?> GetByIdAsync(Guid seedId, CancellationToken cancellationToken);
}
