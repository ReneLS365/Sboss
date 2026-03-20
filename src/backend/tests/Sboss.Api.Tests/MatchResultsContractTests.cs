using System.Net;
using System.Net.Http.Json;
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
            1200,
            100000,
            10,
            0);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(payload);
        Assert.Equal("accepted", payload!.ValidationStatus);
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
            1200,
            100000,
            10,
            0);

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
            1200,
            100000,
            10,
            0);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
