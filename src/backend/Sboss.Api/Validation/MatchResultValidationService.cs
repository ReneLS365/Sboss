using Sboss.Contracts.Common;
using Sboss.Contracts.MatchResults;

namespace Sboss.Api.Validation;

public interface IMatchResultValidationService
{
    Task<string> ValidateAsync(PostMatchResultRequest request, CancellationToken cancellationToken);
}

public sealed class MatchResultValidationService : IMatchResultValidationService
{
    public Task<string> ValidateAsync(PostMatchResultRequest request, CancellationToken cancellationToken)
    {
        if (request.Score < 0 || request.ClearTimeMs <= 0 || request.ComboMax < 0 || request.Penalties < 0)
        {
            return Task.FromResult(ValidationStatuses.Rejected);
        }

        return Task.FromResult(ValidationStatuses.Accepted);
    }
}
