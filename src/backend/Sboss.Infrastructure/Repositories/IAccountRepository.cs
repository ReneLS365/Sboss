using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);
}
