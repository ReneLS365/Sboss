namespace Sboss.Contracts.Loadout;

public sealed record PostLoadoutSubmissionRequest(IReadOnlyList<LoadoutItemRequest> Items);

public sealed record LoadoutItemRequest(string ItemCode, int Quantity);
