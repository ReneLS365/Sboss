using Sboss.Contracts.Seasons;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemorySeasonRepository : ISeasonRepository
{
    public Task<CurrentSeasonResponse> GetCurrentSeasonAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var season = new CurrentSeasonResponse(
            Guid.Parse("11111111-aaaa-4444-bbbb-111111111111"),
            "Phase0-Season",
            now.AddDays(-1),
            now.AddDays(30),
            true,
            1);
        return Task.FromResult(season);
    }
}
