namespace Sboss.Contracts.LevelSeeds;

public sealed record LevelSeedResponse(
    Guid LevelSeedId,
    string SeedValue,
    string Biome,
    string Template,
    string Objective,
    string ModifiersJson,
    int ParTimeMs,
    int GoldTimeMs,
    int Version);
