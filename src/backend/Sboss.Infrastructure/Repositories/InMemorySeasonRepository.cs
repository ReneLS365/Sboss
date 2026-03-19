using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class InMemorySeasonRepository : ISeasonRepository
{
    private static readonly Guid ActiveSeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public Task<Season> GetCurrentSeasonAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddDays(-2);
        var season = Season.Rehydrate(
            ActiveSeasonId,
            "Phase0-Season",
            now.AddDays(-1),
            now.AddDays(30),
            true,
            createdAt,
            createdAt,
            1);

        return Task.FromResult(season);
    }
}
