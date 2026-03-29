namespace Sboss.Infrastructure.Services;

public interface IEconomyTransactionService
{
    Task<EconomyTransactionResult> ApplyAsync(EconomyMutationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EconomyTransactionResult>> ApplyBatchAsync(
        IReadOnlyList<EconomyMutationRequest> requests,
        CancellationToken cancellationToken);
}
