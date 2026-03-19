using Sboss.Contracts.Seasons;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemorySeasonRepository : ISeasonRepository
{
    private static readonly Guid ActiveSeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public Task<CurrentSeasonResponse> GetCurrentSeasonAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var season = new CurrentSeasonResponse(
            ActiveSeasonId,
            "Phase0-Season",
            now.AddDays(-1),
            now.AddDays(30),
            true,
            1);
        return Task.FromResult(season);
    }
}
