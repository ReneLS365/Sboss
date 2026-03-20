namespace Sboss.Infrastructure.Services;

public sealed record EconomyMutationRequest(
    Guid AccountId,
    string CurrencyCode,
    long AmountDelta,
    string IdempotencyKey,
    string Reason);
