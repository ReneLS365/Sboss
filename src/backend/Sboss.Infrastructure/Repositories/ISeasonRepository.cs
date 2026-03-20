using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface ISeasonRepository
{
    Task<Season> GetCurrentSeasonAsync(CancellationToken cancellationToken);
    Task<Season?> GetByIdAsync(Guid seasonId, CancellationToken cancellationToken);
}
