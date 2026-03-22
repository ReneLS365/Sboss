using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed record SubmitContractJobApplicationRequest(
    Guid ContractJobId,
    Guid ApplicantAccountId,
    string IdempotencyKey);

public sealed record WithdrawContractJobApplicationRequest(
    Guid ContractJobId,
    Guid ApplicationId,
    string IdempotencyKey);

public sealed record AcceptContractJobApplicationRequest(
    Guid ContractJobId,
    Guid ApplicationId,
    string IdempotencyKey);

public sealed record ContractJobApplicationMutationResult(
    ContractJobApplication Application,
    bool IsIdempotentReplay,
    ContractJobState? ResultingJobState,
    Guid? AcceptedApplicationId);
