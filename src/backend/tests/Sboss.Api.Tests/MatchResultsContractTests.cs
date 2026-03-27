using System.Net;
using System.Net.Http.Json;
using Sboss.Contracts.Commands;
using Sboss.Contracts.MatchResults;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class MatchResultsContractTests
{
    private readonly TestWebApplicationFactory _factory;

    public MatchResultsContractTests(PostgresDatabaseFixture database)
    {
        _factory = new TestWebApplicationFactory(database.ConnectionString);
    }

    [Fact]
    public async Task PostMatchResult_ReturnsCreatedAndValidationStatus()
    {
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatePlacementIntents(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "frame-a", "deck-a", "diag-a"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(payload);
        Assert.Equal("accepted", payload!.ValidationStatus);
        Assert.True(payload.Score > 0);
        Assert.True(payload.ComboMax > 0);
        Assert.True(payload.StabilityPercent > 0);
        Assert.Equal(2, payload.Version);
    }

    [Fact]
    public async Task PostMatchResult_WithInvalidDomainPayload_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Empty,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatePlacementIntents(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "frame-a"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMatchResult_WithUnknownReferences_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            CreatePlacementIntents(Guid.Parse("77777777-7777-7777-7777-777777777777"), "frame-a"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMatchResult_IgnoresCallerReportedScoreAndReturnsAuthoritativeScore()
    {
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatePlacementIntents(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "frame-a", "deck-a", "diag-a"),
            999999);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(request.ReportedScore, payload!.Score);
    }

    [Fact]
    public async Task PostMatchResult_IsDeterministicForSameAuthoritativeInput()
    {
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "frame-a", "deck-a", "diag-a", "component-not-present"),
            1);

        var first = await client.PostAsJsonAsync("/api/v1/match-results", request);
        var second = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstPayload = await first.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        var secondPayload = await second.Content.ReadFromJsonAsync<PostMatchResultResponse>();

        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(firstPayload!.Score, secondPayload!.Score);
        Assert.Equal(firstPayload.ComboMax, secondPayload.ComboMax);
        Assert.Equal(firstPayload.StabilityPercent, secondPayload.StabilityPercent);
        Assert.Equal(firstPayload.Penalties, secondPayload.Penalties);
    }

    private static IReadOnlyList<PlaceComponentIntent> CreatePlacementIntents(Guid seedId, params string[] componentIds)
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return componentIds
            .Select((componentId, index) => new PlaceComponentIntent
            {
                SeedId = seedId,
                ComponentId = componentId,
                Timestamp = timestamp.AddMilliseconds(index)
            })
            .ToArray();
    }
}
