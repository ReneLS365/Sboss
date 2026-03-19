using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Sboss.Contracts.LevelSeeds;

namespace Sboss.Api.Tests;

public sealed class LevelSeedsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid KnownSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly WebApplicationFactory<Program> _factory;

    public LevelSeedsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
