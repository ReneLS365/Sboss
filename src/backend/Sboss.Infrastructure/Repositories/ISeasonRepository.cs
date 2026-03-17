using Sboss.Contracts.Seasons;

namespace Sboss.Infrastructure.Repositories;

public interface ISeasonRepository
{
    Task<CurrentSeasonResponse> GetCurrentSeasonAsync(CancellationToken cancellationToken);
}
