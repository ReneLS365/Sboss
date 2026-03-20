using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface IMatchResultRepository
{
    Task<MatchResult> SaveAsync(MatchResult matchResult, CancellationToken cancellationToken);
    Task<MatchResult?> GetByIdAsync(Guid matchResultId, CancellationToken cancellationToken);
}
