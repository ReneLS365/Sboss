using Sboss.Contracts.MatchResults;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemoryMatchResultRepository : IMatchResultRepository
{
    public Task<PostMatchResultResponse> SaveAsync(PostMatchResultRequest request, string validationStatus, CancellationToken cancellationToken)
    {
        var response = new PostMatchResultResponse(
            Guid.NewGuid(),
            validationStatus,
            DateTimeOffset.UtcNow,
            1);

        return Task.FromResult(response);
    }
}
