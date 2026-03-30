namespace Sboss.Contracts.FogOfWar;

public sealed record PostFogRevealResponse(
    Guid AccountId,
    Guid LevelSeedId,
    string RevealKey,
    string Status,
    IReadOnlyList<string> RevealedKeys);
