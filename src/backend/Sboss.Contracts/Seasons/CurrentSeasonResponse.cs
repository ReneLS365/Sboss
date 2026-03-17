namespace Sboss.Contracts.Seasons;

public sealed record CurrentSeasonResponse(
    Guid SeasonId,
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive,
    long Version);
