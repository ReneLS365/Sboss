namespace Sboss.Contracts.Economy;

public sealed record PostEconomyTransactionResponse(
    Guid EconomyTransactionId,
    Guid AccountId,
    string CurrencyCode,
    long AmountDelta,
    long ResultingBalance,
    string Reason,
    string Outcome,
    DateTimeOffset CreatedAt,
    long BalanceVersion,
    long TransactionVersion);
