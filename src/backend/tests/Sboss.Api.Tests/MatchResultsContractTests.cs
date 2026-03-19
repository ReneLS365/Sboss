using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Sboss.Contracts.MatchResults;

namespace Sboss.Api.Tests;

public sealed class MatchResultsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MatchResultsContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
}
