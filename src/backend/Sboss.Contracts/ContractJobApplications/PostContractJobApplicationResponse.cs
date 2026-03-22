namespace Sboss.Contracts.ContractJobApplications;

public sealed record PostContractJobApplicationResponse(
    Guid ApplicationId,
    Guid ContractJobId,
    Guid ApplicantAccountId,
    string ApplicationStatus,
    string Outcome,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version,
    string? ResultingJobState,
    Guid? AcceptedApplicationId);
