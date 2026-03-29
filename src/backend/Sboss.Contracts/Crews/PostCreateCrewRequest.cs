namespace Sboss.Contracts.Crews;

public sealed record PostCreateCrewRequest(Guid OwnerAccountId, string Name);
