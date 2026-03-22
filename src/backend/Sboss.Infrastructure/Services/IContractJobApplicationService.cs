namespace Sboss.Infrastructure.Services;

public interface IContractJobApplicationService
{
    Task<ContractJobApplicationMutationResult> SubmitApplicationAsync(SubmitContractJobApplicationRequest request, CancellationToken cancellationToken);
    Task<ContractJobApplicationMutationResult> WithdrawApplicationAsync(WithdrawContractJobApplicationRequest request, CancellationToken cancellationToken);
    Task<ContractJobApplicationMutationResult> AcceptApplicationAsync(AcceptContractJobApplicationRequest request, CancellationToken cancellationToken);
}
