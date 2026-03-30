namespace Sboss.Contracts.Loadout;

public sealed record GetLoadoutStateResponse(
    Guid AccountId,
    Guid LevelSeedId,
    int MaxCapacity,
    int UsedCapacity,
    bool IsComplete,
    IReadOnlyList<LoadoutItemResponse> Items,
    IReadOnlyList<string> MissingRequiredComponents);

public sealed record LoadoutItemResponse(string ItemCode, int Quantity);
