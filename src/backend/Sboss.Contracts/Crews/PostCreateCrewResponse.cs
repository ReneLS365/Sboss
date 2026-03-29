namespace Sboss.Contracts.Crews;

public sealed record PostCreateCrewResponse(
    Guid CrewId,
    Guid OwnerAccountId,
    string Name,
    DateTimeOffset CreatedAt,
    long Version);
