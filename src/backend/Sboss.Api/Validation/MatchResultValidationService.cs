using Sboss.Domain.Entities;

namespace Sboss.Api.Validation;

public interface IMatchResultValidationService
{
    Task<MatchValidationStatus> ValidateAsync(MatchResult matchResult, CancellationToken cancellationToken);
}

public sealed class MatchResultValidationService : IMatchResultValidationService
{
    public Task<MatchValidationStatus> ValidateAsync(MatchResult matchResult, CancellationToken cancellationToken)
    {
        if (matchResult.Score < 0 || matchResult.ClearTimeMs <= 0 || matchResult.ComboMax < 0 || matchResult.Penalties < 0)
        {
            return Task.FromResult(MatchValidationStatus.Rejected);
        }

        return Task.FromResult(MatchValidationStatus.Accepted);
    }
}
