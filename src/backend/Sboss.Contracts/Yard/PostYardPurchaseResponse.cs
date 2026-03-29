namespace Sboss.Contracts.Yard;

public sealed record PostYardPurchaseResponse(
    Guid AccountId,
    string ItemCode,
    int PurchasedQuantity,
    long UnitPurchaseCost,
    long TotalPurchaseCost,
    long RemainingCoinBalance,
    int MaxCapacity,
    int UsedCapacity,
    int RemainingCapacity,
    int OwnedQuantity);
