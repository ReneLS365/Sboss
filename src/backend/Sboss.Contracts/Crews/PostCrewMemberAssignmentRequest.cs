namespace Sboss.Contracts.Crews;

public sealed record PostCrewMemberAssignmentRequest(Guid ActorAccountId, Guid MemberAccountId, string Role);
