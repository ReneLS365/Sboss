namespace Sboss.Contracts.MatchResults;

public sealed record PostMatchResultResponse(
    Guid MatchResultId,
    string ValidationStatus,
    DateTimeOffset CreatedAt,
    long Version);
