using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Sboss.Contracts.Seasons;

namespace Sboss.Api.Tests;

public sealed class SeasonEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid ActiveSeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly WebApplicationFactory<Program> _factory;

    public SeasonEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
