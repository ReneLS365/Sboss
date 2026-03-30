namespace Sboss.Infrastructure.Services;

public sealed record ProgressionState(
    Guid AccountId,
    long TotalXp,
    int Level,
    long Version);

public sealed record ProgressionAwardResult(
    Guid AccountId,
    Guid MatchResultId,
    int XpAwarded,
    long TotalXp,
    int Level,
    bool LeveledUp,
    bool IsIdempotentReplay);
