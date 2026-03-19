using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface ILevelSeedRepository
{
    Task<LevelSeed?> GetByIdAsync(Guid seedId, CancellationToken cancellationToken);
}
