using Sboss.Contracts.LevelSeeds;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemoryLevelSeedRepository : ILevelSeedRepository
{
    public Task<LevelSeedResponse?> GetByIdAsync(Guid seedId, CancellationToken cancellationToken)
    {
        var seed = new LevelSeedResponse(
            seedId,
            "SBOSS-SEED-001",
            "urban",
            "template_alpha",
            "reach_target",
            "{\"modifiers\":[\"none\"]}",
            120000,
            90000,
            1);

        return Task.FromResult<LevelSeedResponse?>(seed);
    }
}
