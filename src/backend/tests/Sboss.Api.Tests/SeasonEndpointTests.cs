using System.Net;
using System.Net.Http.Json;
using Sboss.Contracts.Seasons;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class SeasonEndpointTests
{
    private static readonly Guid ActiveSeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly TestWebApplicationFactory _factory;

    public SeasonEndpointTests(PostgresDatabaseFixture database)
    {
        _factory = new TestWebApplicationFactory(database.ConnectionString);
    }

    [Fact]
    public async Task GetCurrentSeason_ReturnsActiveSeededSeason()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/seasons/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CurrentSeasonResponse>();
        Assert.NotNull(payload);
        Assert.Equal(ActiveSeasonId, payload!.SeasonId);
        Assert.Equal("Phase0-Season", payload.Name);
        Assert.True(payload.IsActive);
        Assert.True(payload.StartsAt < payload.EndsAt);
    }
}
