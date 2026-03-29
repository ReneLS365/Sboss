using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public interface ICrewService
{
    Task<Crew> CreateCrewAsync(Guid ownerAccountId, string name, CancellationToken cancellationToken);
    Task<CrewMember> AssignMemberAsync(Guid crewId, Guid actorAccountId, Guid memberAccountId, CrewRole role, CancellationToken cancellationToken);
    Task RemoveMemberAsync(Guid crewId, Guid actorAccountId, Guid memberAccountId, CancellationToken cancellationToken);
    Task<CrewSplitResult> PreviewSplitAsync(Guid crewId, long grossAmount, CancellationToken cancellationToken);
    Task<CrewSplitResult> SettlePayoutAsync(Guid crewId, Guid actorAccountId, long grossAmount, string currencyCode, string idempotencyKey, string reason, CancellationToken cancellationToken);
}
