namespace Sboss.Contracts.Yard;

public sealed record PostYardPurchaseRequest(
    string ItemCode,
    int Quantity);
