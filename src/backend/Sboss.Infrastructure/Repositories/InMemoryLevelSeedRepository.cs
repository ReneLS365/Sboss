using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemoryLevelSeedRepository : ILevelSeedRepository
{
    private static readonly Guid KnownSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public Task<LevelSeed?> GetByIdAsync(Guid seedId, CancellationToken cancellationToken)
    {
        if (seedId != KnownSeedId)
        {
            return Task.FromResult<LevelSeed?>(null);
        }

        var createdAt = DateTimeOffset.UtcNow.AddDays(-2);
        var seed = LevelSeed.Rehydrate(
            KnownSeedId,
            "SBOSS-SEED-001",
            "urban",
            "template_alpha",
            "reach_target",
            "{\"modifiers\":[\"none\"]}",
            120000,
            90000,
            1,
            createdAt,
            createdAt);

        return Task.FromResult<LevelSeed?>(seed);
    }
}
