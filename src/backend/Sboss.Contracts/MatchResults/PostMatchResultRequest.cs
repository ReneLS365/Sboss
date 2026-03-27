using Sboss.Contracts.Commands;

namespace Sboss.Contracts.MatchResults;

public sealed record PostMatchResultRequest(
    Guid AccountId,
    Guid SeasonId,
    Guid LevelSeedId,
    IReadOnlyList<PlaceComponentIntent> PlacementIntents,
    int? ReportedScore);
