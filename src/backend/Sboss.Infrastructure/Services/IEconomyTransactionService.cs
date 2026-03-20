namespace Sboss.Infrastructure.Services;

public interface IEconomyTransactionService
{
    Task<EconomyTransactionResult> ApplyAsync(EconomyMutationRequest request, CancellationToken cancellationToken);
}
