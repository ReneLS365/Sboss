namespace Sboss.Contracts.Crews;

public sealed record PostCrewPayoutRequest(
    Guid ActorAccountId,
    long GrossAmount,
    string CurrencyCode,
    string IdempotencyKey,
    string Reason);
