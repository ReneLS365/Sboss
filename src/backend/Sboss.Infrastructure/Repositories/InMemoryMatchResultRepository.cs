using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemoryMatchResultRepository : IMatchResultRepository
{
    public Task<MatchResult> SaveAsync(MatchResult matchResult, CancellationToken cancellationToken)
    {
        return Task.FromResult(matchResult);
    }
}
