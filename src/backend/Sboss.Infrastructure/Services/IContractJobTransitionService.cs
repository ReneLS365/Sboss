namespace Sboss.Infrastructure.Services;

public interface IContractJobTransitionService
{
    Task<ContractJobTransitionResult> TransitionAsync(ContractJobTransitionRequest request, CancellationToken cancellationToken);
}
