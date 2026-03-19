using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface ISeasonRepository
{
    Task<Season> GetCurrentSeasonAsync(CancellationToken cancellationToken);
}
