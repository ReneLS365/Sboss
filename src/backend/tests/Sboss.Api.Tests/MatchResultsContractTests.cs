using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Sboss.Contracts.Commands;
using Sboss.Contracts.MatchResults;
using Sboss.Contracts.Yard;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class MatchResultsContractTests
{
    private readonly TestWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public MatchResultsContractTests(PostgresDatabaseFixture database)
    {
        _database = database;
        _factory = new TestWebApplicationFactory(database.ConnectionString);
    }

    [Fact]
    public async Task PostMatchResult_ReturnsCreatedAndValidationStatus()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatePlacementIntents(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "scaffold_blue_frame", "scaffold_yellow_deck", "scaffold_red_diagonal"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(payload);
        Assert.Equal("accepted", payload!.ValidationStatus);
        Assert.True(payload.Score > 0);
        Assert.True(payload.ComboMax > 0);
        Assert.True(payload.StabilityPercent > 0);
        Assert.Equal(3, payload.ValidationResults.Count);
        Assert.Equal(new[] { true, true, false }, payload.ValidationResults.Select(result => result.Accepted).ToArray());
        Assert.Equal("yard_capacity_exceeded", payload.ValidationResults[^1].Code);
        Assert.Equal(2, payload.Version);
    }

    [Fact]
    public async Task PostMatchResult_WithInvalidDomainPayload_ReturnsBadRequest()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Empty,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatePlacementIntents(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "scaffold_blue_frame"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMatchResult_WithUnknownReferences_ReturnsBadRequest()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            CreatePlacementIntents(Guid.Parse("77777777-7777-7777-7777-777777777777"), "scaffold_blue_frame"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMatchResult_IgnoresCallerReportedScoreAndReturnsAuthoritativeScore()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CreatePlacementIntents(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "scaffold_blue_frame", "scaffold_yellow_deck", "scaffold_red_diagonal"),
            999999);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(request.ReportedScore, payload!.Score);
        Assert.Equal(3, payload.ValidationResults.Count);
        Assert.Equal(new[] { true, true, false }, payload.ValidationResults.Select(result => result.Accepted).ToArray());
        Assert.Equal("yard_capacity_exceeded", payload.ValidationResults[^1].Code);
    }

    [Fact]
    public async Task PostMatchResult_IsDeterministicForSameAuthoritativeInput()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_blue_frame", "scaffold_yellow_deck", "scaffold_red_diagonal", "component-not-present"),
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
        Assert.Equal(firstPayload.ValidationResults, secondPayload.ValidationResults);
    }

    [Fact]
    public async Task PostMatchResult_WithNullPlacementIntentEntry_ReturnsBadRequest()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var payload = """
        {
          "accountId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          "seasonId":"cccccccc-cccc-cccc-cccc-cccccccccccc",
          "levelSeedId":"dddddddd-dddd-dddd-dddd-dddddddddddd",
          "placementIntents":[null]
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/match-results", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("placementIntents"));
    }

    [Fact]
    public async Task PostMatchResult_RejectsPlacementsThatExceedSequenceCapacity()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_blue_frame", "scaffold_blue_frame", "scaffold_blue_frame"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.ComboMax);
        Assert.Equal(1, result.Penalties);
        Assert.Equal(67, result.StabilityPercent);
        Assert.Equal(3, result.ValidationResults.Count);
        Assert.Equal(new[] { true, true, false }, result.ValidationResults.Select(r => r.Accepted).ToArray());
        Assert.Equal("yard_capacity_exceeded", result.ValidationResults[^1].Code);
    }

    [Fact]
    public async Task PostMatchResult_RejectsOutOfOrderAssemblySequenceUsingServerAuthoritativeRules()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_yellow_deck", "scaffold_blue_frame", "scaffold_red_diagonal"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Penalties);
        Assert.Equal(33, result.StabilityPercent);
        Assert.Equal(1, result.ComboMax);
        Assert.Equal(new[] { false, true, false }, result.ValidationResults.Select(r => r.Accepted).ToArray());
        Assert.Equal("scaffold_assembly_invalid_sequence", result.ValidationResults[0].Code);
        Assert.Equal("scaffold_assembly_invalid_sequence", result.ValidationResults[2].Code);
    }

    [Fact]
    public async Task PostMatchResult_RejectsPlacementsWhenOwnedInventoryIsInsufficient()
    {
        await _database.ResetAsync();
        var accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SetInventoryQuantityAsync(accountId, "scaffold_blue_frame", 0);

        try
        {
            var client = _factory.CreateClient();
            var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
            var request = new PostMatchResultRequest(
                accountId,
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                seedId,
                CreatePlacementIntents(seedId, "scaffold_blue_frame"),
                null);

            var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
            Assert.NotNull(result);
            Assert.Single(result!.ValidationResults);
            Assert.False(result.ValidationResults[0].Accepted);
            Assert.Equal("inventory_insufficient", result.ValidationResults[0].Code);
        }
        finally
        {
            await SetInventoryQuantityAsync(accountId, "scaffold_blue_frame", 3);
        }
    }

    [Fact]
    public async Task PostMatchResult_InvalidSequenceAppliesWearToOwnedKnownComponent()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_yellow_deck"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var yard = await client.GetFromJsonAsync<GetYardStateResponse>("/api/v1/yard/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Assert.NotNull(yard);
        var yellow = yard!.Inventory.Single(item => item.ItemCode == "scaffold_yellow_deck");
        Assert.Equal(1, yellow.OwnedQuantity);
        Assert.Equal(7500, yellow.TotalIntegrityBps);
        Assert.Equal(0, yellow.UsableQuantity);
        Assert.Equal(1, yellow.DamagedQuantity);
    }

    [Fact]
    public async Task PostMatchResult_CapacityOverflowDoesNotApplyWear()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_blue_frame", "scaffold_blue_frame", "scaffold_blue_frame"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var yard = await client.GetFromJsonAsync<GetYardStateResponse>("/api/v1/yard/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Assert.NotNull(yard);
        var blue = yard!.Inventory.Single(item => item.ItemCode == "scaffold_blue_frame");
        Assert.Equal(30000, blue.TotalIntegrityBps);
    }

    [Fact]
    public async Task PostMatchResult_InventoryInsufficientDoesNotApplyWear()
    {
        await _database.ResetAsync();
        var accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SetInventoryStateAsync(accountId, "scaffold_blue_frame", 0, 0);
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            accountId,
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_blue_frame"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostMatchResultResponse>();
        Assert.NotNull(body);
        Assert.Equal("inventory_insufficient", body!.ValidationResults[0].Code);

        var yard = await client.GetFromJsonAsync<GetYardStateResponse>($"/api/v1/yard/{accountId}");
        Assert.NotNull(yard);
        var blue = yard!.Inventory.Single(item => item.ItemCode == "scaffold_blue_frame");
        Assert.Equal(0, blue.TotalIntegrityBps);
        Assert.Equal(0, blue.OwnedQuantity);
    }

    [Fact]
    public async Task PostMatchResult_UnknownComponentDoesNotApplyWear()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "component-not-present"),
            null);

        var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var yard = await client.GetFromJsonAsync<GetYardStateResponse>("/api/v1/yard/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Assert.NotNull(yard);
        var blue = yard!.Inventory.Single(item => item.ItemCode == "scaffold_blue_frame");
        Assert.Equal(30000, blue.TotalIntegrityBps);
    }

    [Fact]
    public async Task PostMatchResult_RepeatedInvalidSequenceWearLowersUsableBelowOwned()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();
        var seedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var request = new PostMatchResultRequest(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            seedId,
            CreatePlacementIntents(seedId, "scaffold_blue_frame", "scaffold_blue_frame"),
            null);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/match-results", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var yard = await client.GetFromJsonAsync<GetYardStateResponse>("/api/v1/yard/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Assert.NotNull(yard);
        var blue = yard!.Inventory.Single(item => item.ItemCode == "scaffold_blue_frame");
        Assert.Equal(3, blue.OwnedQuantity);
        Assert.Equal(17500, blue.TotalIntegrityBps);
        Assert.Equal(1, blue.UsableQuantity);
        Assert.Equal(2, blue.DamagedQuantity);
    }

    private async Task SetInventoryQuantityAsync(Guid accountId, string itemCode, int quantity)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE inventory_items
            SET quantity = @quantity,
                total_integrity_bps = @quantity * 10000,
                updated_at = NOW(),
                version = version + 1
            WHERE account_id = @accountId
              AND item_code = @itemCode;
            """;
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("itemCode", itemCode);
        command.Parameters.AddWithValue("quantity", quantity);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetInventoryStateAsync(Guid accountId, string itemCode, int quantity, long totalIntegrityBps)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE inventory_items
            SET quantity = @quantity,
                total_integrity_bps = @totalIntegrityBps,
                updated_at = NOW(),
                version = version + 1
            WHERE account_id = @accountId
              AND item_code = @itemCode;
            """;
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("itemCode", itemCode);
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("totalIntegrityBps", totalIntegrityBps);
        await command.ExecuteNonQueryAsync();
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
