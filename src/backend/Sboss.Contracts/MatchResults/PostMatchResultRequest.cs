namespace Sboss.Contracts.MatchResults;

public sealed record PostMatchResultRequest(
    Guid AccountId,
    Guid SeasonId,
    Guid LevelSeedId,
    int Score,
    int ClearTimeMs,
    int ComboMax,
    int Penalties);
