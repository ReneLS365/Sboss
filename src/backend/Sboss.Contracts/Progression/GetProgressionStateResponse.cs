namespace Sboss.Contracts.Progression;

public sealed record GetProgressionStateResponse(
    Guid AccountId,
    long TotalXp,
    int Level,
    long? NextLevelXpRequired,
    IReadOnlyList<string> UnlockedTemplates);
