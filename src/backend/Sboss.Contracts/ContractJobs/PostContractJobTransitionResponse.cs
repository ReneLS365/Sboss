namespace Sboss.Contracts.ContractJobs;

public sealed record PostContractJobTransitionResponse(
    Guid ContractJobId,
    Guid OwningAccountId,
    string CurrentState,
    string Outcome,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version);
