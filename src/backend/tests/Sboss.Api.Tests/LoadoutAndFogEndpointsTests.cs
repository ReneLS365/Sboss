using System.Net;
using System.Net.Http.Json;
using Sboss.Contracts.FogOfWar;
using Sboss.Contracts.Loadout;
using Sboss.Contracts.MatchResults;
using Sboss.Contracts.Commands;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class LoadoutAndFogEndpointsTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid SeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly TestWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public LoadoutAndFogEndpointsTests(PostgresDatabaseFixture database)
    {
        _database = database;
        _factory = new TestWebApplicationFactory(database.ConnectionString);
    }

    [Fact]
    public async Task PostLoadout_ValidRequest_IsAccepted()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/loadout/{AccountId}/{SeedId}",
            new PostLoadoutSubmissionRequest(new[]
            {
                new LoadoutItemRequest("scaffold_blue_frame", 1),
                new LoadoutItemRequest("scaffold_yellow_deck", 1),
                new LoadoutItemRequest("scaffold_red_diagonal", 1)
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostLoadoutSubmissionResponse>();
        Assert.NotNull(body);
        Assert.Equal("accepted", body!.Status);
        Assert.True(body.IsComplete);
        Assert.Empty(body.MissingRequiredComponents);
    }

    [Fact]
    public async Task PostLoadout_CapacityOverflow_ReturnsBadRequest()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/loadout/{AccountId}/{SeedId}",
            new PostLoadoutSubmissionRequest(new[]
            {
                new LoadoutItemRequest("scaffold_red_diagonal", 3)
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostLoadout_MissingRequiredComponent_IsIncomplete()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/loadout/{AccountId}/{SeedId}",
            new PostLoadoutSubmissionRequest(new[]
            {
                new LoadoutItemRequest("scaffold_blue_frame", 1),
                new LoadoutItemRequest("scaffold_yellow_deck", 1)
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostLoadoutSubmissionResponse>();
        Assert.NotNull(body);
        Assert.Equal("incomplete", body!.Status);
        Assert.Contains("scaffold_red_diagonal", body.MissingRequiredComponents);
    }

    [Fact]
    public async Task Fog_InitialState_IsEmpty()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<GetFogStateResponse>($"/api/v1/fog/{AccountId}/{SeedId}");
        Assert.NotNull(response);
        Assert.Empty(response!.RevealedKeys);
    }

    [Fact]
    public async Task Fog_Reveal_IsDeterministicAndIdempotent()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync($"/api/v1/fog/{AccountId}/{SeedId}/reveal", new PostFogRevealRequest("zone_a"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<PostFogRevealResponse>();
        Assert.NotNull(firstBody);
        Assert.Equal("revealed", firstBody!.Status);
        Assert.Contains("zone_a", firstBody.RevealedKeys);

        var second = await client.PostAsJsonAsync($"/api/v1/fog/{AccountId}/{SeedId}/reveal", new PostFogRevealRequest("zone_a"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<PostFogRevealResponse>();
        Assert.NotNull(secondBody);
        Assert.Equal("already_revealed", secondBody!.Status);
        Assert.Single(secondBody.RevealedKeys);
    }

    [Fact]
    public async Task Fog_Reveal_WithUnknownReferences_ReturnsNotFound()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var unknownAccountId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var unknownSeedId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var response = await client.PostAsJsonAsync($"/api/v1/fog/{unknownAccountId}/{unknownSeedId}/reveal", new PostFogRevealRequest("zone_a"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MatchResults_FailsPlacementWhenComponentMissingFromApprovedLoadout()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var submit = await client.PostAsJsonAsync($"/api/v1/loadout/{AccountId}/{SeedId}",
            new PostLoadoutSubmissionRequest(new[]
            {
                new LoadoutItemRequest("scaffold_blue_frame", 1),
                new LoadoutItemRequest("scaffold_yellow_deck", 1)
            }));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var request = new PostMatchResultRequest(
            AccountId,
            SeasonId,
            SeedId,
            new[]
            {
                new PlaceComponentIntent
                {
                    SeedId = SeedId,
                    ComponentId = "scaffold_red_diagonal",
                    Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
                }
            },
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(body);
        Assert.False(body!.ValidationResults[0].Accepted);
        Assert.Equal("loadout_missing_component", body.ValidationResults[0].Code);
    }
}
