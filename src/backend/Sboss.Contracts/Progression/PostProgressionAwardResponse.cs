namespace Sboss.Contracts.Progression;

public sealed record PostProgressionAwardResponse(
    Guid AccountId,
    Guid MatchResultId,
    int XpAwarded,
    long TotalXp,
    int Level,
    bool LeveledUp,
    string Status);
