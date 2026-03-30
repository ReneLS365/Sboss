namespace Sboss.Contracts.Loadout;

public sealed record PostLoadoutSubmissionResponse(
    Guid AccountId,
    Guid LevelSeedId,
    int MaxCapacity,
    int UsedCapacity,
    bool IsComplete,
    string Status,
    IReadOnlyList<string> MissingRequiredComponents,
    IReadOnlyList<LoadoutItemResponse> Items);
