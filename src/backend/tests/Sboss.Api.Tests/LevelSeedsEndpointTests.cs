using System.Net;
using System.Net.Http.Json;
using Sboss.Contracts.LevelSeeds;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class LevelSeedsEndpointTests
{
    private static readonly Guid KnownSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly TestWebApplicationFactory _factory;

    public LevelSeedsEndpointTests(PostgresDatabaseFixture database)
    {
        _factory = new TestWebApplicationFactory(database.ConnectionString);
    }

    [Fact]
    public async Task GetLevelSeed_WithUnknownId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/level-seeds/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLevelSeed_WithKnownId_ReturnsSeedPayload()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/level-seeds/{KnownSeedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LevelSeedResponse>();
        Assert.NotNull(payload);
        Assert.Equal(KnownSeedId, payload!.LevelSeedId);
        Assert.Equal("SBOSS-SEED-001", payload.SeedValue);
    }
}
