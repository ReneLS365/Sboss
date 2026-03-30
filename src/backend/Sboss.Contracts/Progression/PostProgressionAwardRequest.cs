namespace Sboss.Contracts.Progression;

public sealed record PostProgressionAwardRequest(
    Guid AccountId,
    Guid MatchResultId);
