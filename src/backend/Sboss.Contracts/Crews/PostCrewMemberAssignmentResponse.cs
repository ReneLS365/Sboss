namespace Sboss.Contracts.Crews;

public sealed record PostCrewMemberAssignmentResponse(
    Guid CrewId,
    Guid MemberAccountId,
    string Role,
    DateTimeOffset UpdatedAt,
    long Version);
