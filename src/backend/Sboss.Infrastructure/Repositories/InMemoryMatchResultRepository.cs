using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemoryMatchResultRepository : IMatchResultRepository
{
    private readonly Dictionary<Guid, MatchResult> _results = new();

    public Task<MatchResult> SaveAsync(MatchResult matchResult, CancellationToken cancellationToken)
    {
        _results[matchResult.MatchResultId] = matchResult;
        return Task.FromResult(matchResult);
    }

    public Task<MatchResult?> GetByIdAsync(Guid matchResultId, CancellationToken cancellationToken)
    {
        _results.TryGetValue(matchResultId, out var matchResult);
        return Task.FromResult(matchResult);
    }
}
