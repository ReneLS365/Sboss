namespace Sboss.Contracts.Crews;

public sealed record CrewMemberBreakdownResponse(Guid AccountId, string Role, int RatioWeight, long Amount);
