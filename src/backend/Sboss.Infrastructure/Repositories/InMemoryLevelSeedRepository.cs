using Sboss.Contracts.LevelSeeds;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemoryLevelSeedRepository : ILevelSeedRepository
{
    private static readonly Guid KnownSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public Task<LevelSeedResponse?> GetByIdAsync(Guid seedId, CancellationToken cancellationToken)
    {
        if (seedId != KnownSeedId)
        {
            return Task.FromResult<LevelSeedResponse?>(null);
        }

        var seed = new LevelSeedResponse(
            KnownSeedId,
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
