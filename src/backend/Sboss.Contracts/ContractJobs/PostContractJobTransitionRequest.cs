namespace Sboss.Contracts.ContractJobs;

public sealed record PostContractJobTransitionRequest(
    string TargetState,
    string IdempotencyKey);
