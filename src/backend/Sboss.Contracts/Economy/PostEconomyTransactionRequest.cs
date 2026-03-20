namespace Sboss.Contracts.Economy;

public sealed record PostEconomyTransactionRequest(
    Guid AccountId,
    string CurrencyCode,
    long AmountDelta,
    string IdempotencyKey,
    string Reason);
