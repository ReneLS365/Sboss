using Sboss.Contracts.Commands;

namespace Sboss.Contracts.MatchResults;

public sealed record PostMatchResultResponse(
    Guid MatchResultId,
    int Score,
    int ComboMax,
    int StabilityPercent,
    int Penalties,
    IReadOnlyList<CommandValidationResult> ValidationResults,
    string ValidationStatus,
    DateTimeOffset CreatedAt,
    long Version);
