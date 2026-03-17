using Sboss.Contracts.MatchResults;

namespace Sboss.Infrastructure.Repositories;

public interface IMatchResultRepository
{
    Task<PostMatchResultResponse> SaveAsync(PostMatchResultRequest request, string validationStatus, CancellationToken cancellationToken);
}
