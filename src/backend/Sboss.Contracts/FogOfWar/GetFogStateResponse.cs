namespace Sboss.Contracts.FogOfWar;

public sealed record GetFogStateResponse(
    Guid AccountId,
    Guid LevelSeedId,
    IReadOnlyList<string> RevealedKeys);
