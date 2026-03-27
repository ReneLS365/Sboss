namespace Sboss.Contracts.MatchResults;

public sealed record PostMatchResultResponse(
    Guid MatchResultId,
    int Score,
    int ComboMax,
    int StabilityPercent,
    int Penalties,
    string ValidationStatus,
    DateTimeOffset CreatedAt,
    long Version);
