using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface IContractJobRepository
{
    Task<ContractJob?> GetByIdAsync(Guid contractJobId, CancellationToken cancellationToken);
}
