namespace Sboss.Contracts.ContractJobApplications;

public sealed record PostContractJobApplicationRequest(
    Guid ApplicantAccountId,
    string IdempotencyKey);
