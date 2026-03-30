namespace Sboss.Contracts.Yard;

public sealed record GetYardStateResponse(
    Guid AccountId,
    int MaxCapacity,
    int UsedCapacity,
    int RemainingCapacity,
    long CoinBalance,
    IReadOnlyList<YardInventoryItemResponse> Inventory);

public sealed record YardInventoryItemResponse(
    string ItemCode,
    int Quantity,
    int OwnedQuantity,
    int UsableQuantity,
    int DamagedQuantity,
    long TotalIntegrityBps,
    int UnitCapacity,
    long PurchaseCost);
