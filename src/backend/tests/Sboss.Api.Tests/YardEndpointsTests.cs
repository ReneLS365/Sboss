using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Sboss.Contracts.Yard;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class YardEndpointsTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly PostgresDatabaseFixture _database;

    public YardEndpointsTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task GetYardState_ReturnsAuthoritativeCapacityAndInventorySnapshot()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/yard/{AccountId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GetYardStateResponse>();
        Assert.NotNull(body);
        Assert.Equal(AccountId, body!.AccountId);
        Assert.Equal(1500, body.MaxCapacity);
        Assert.Equal(9, body.UsedCapacity);
        Assert.Equal(1491, body.RemainingCapacity);
        Assert.Equal(100, body.CoinBalance);
        Assert.Equal(3, body.Inventory.Count);
        Assert.Contains(body.Inventory, item => item.ItemCode == "scaffold_blue_frame" && item.Quantity == 3);
    }

    [Fact]
    public async Task Purchase_RejectsInvalidQuantityUnknownItemAndMissingAccount()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var invalidQuantity = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", 0));
        Assert.Equal(HttpStatusCode.BadRequest, invalidQuantity.StatusCode);

        var unknownItem = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("not_real", 1));
        Assert.Equal(HttpStatusCode.BadRequest, unknownItem.StatusCode);

        var missingAccount = await client.PostAsJsonAsync(
            "/api/v1/yard/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", 1));
        Assert.Equal(HttpStatusCode.NotFound, missingAccount.StatusCode);
    }

    [Fact]
    public async Task Purchase_RejectsInsufficientFundsAndCapacityOverflow()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var insufficientFunds = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_red_diagonal", 5));
        Assert.Equal(HttpStatusCode.BadRequest, insufficientFunds.StatusCode);

        await SetYardCapacityAsync(AccountId, 10);

        var capacityOverflow = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", 1));
        Assert.Equal(HttpStatusCode.BadRequest, capacityOverflow.StatusCode);
    }

    [Fact]
    public async Task Purchase_AppliesAtomicallyAndUpdatesInventoryCapacityAndBalance()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostYardPurchaseResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.PurchasedQuantity);
        Assert.Equal(20, body.TotalPurchaseCost);
        Assert.Equal(80, body.RemainingCoinBalance);
        Assert.Equal(13, body.UsedCapacity);
        Assert.Equal(1487, body.RemainingCapacity);
        Assert.Equal(5, body.OwnedQuantity);
    }

    [Fact]
    public async Task Purchase_ResponseCapacityUsesFullCatalogForMixedInventory()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_red_diagonal", 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostYardPurchaseResponse>();
        Assert.NotNull(body);
        Assert.Equal(14, body!.UsedCapacity);
        Assert.Equal(1486, body.RemainingCapacity);
    }

    [Fact]
    public async Task Purchase_RejectsOversizedQuantityWithClientValidationError()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", int.MaxValue));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Purchase_SerializesCapacityChecksPerAccountUnderConcurrency()
    {
        await _database.ResetAsync();
        await SetYardCapacityAsync(AccountId, 11);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var firstTask = client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", 1));
        var secondTask = client.PostAsJsonAsync(
            $"/api/v1/yard/{AccountId}/purchases",
            new PostYardPurchaseRequest("scaffold_blue_frame", 1));

        await Task.WhenAll(firstTask, secondTask);
        var statuses = new[] { firstTask.Result.StatusCode, secondTask.Result.StatusCode };

        Assert.Single(statuses.Where(status => status == HttpStatusCode.OK));
        Assert.Single(statuses.Where(status => status == HttpStatusCode.BadRequest));

        var yard = await client.GetFromJsonAsync<GetYardStateResponse>($"/api/v1/yard/{AccountId}");
        Assert.NotNull(yard);
        Assert.Equal(11, yard!.MaxCapacity);
        Assert.Equal(4, yard.Inventory.Single(item => item.ItemCode == "scaffold_blue_frame").Quantity);
        Assert.Equal(11, yard.UsedCapacity);
        Assert.Equal(0, yard.RemainingCapacity);
    }

    private async Task SetYardCapacityAsync(Guid accountId, int maxCapacity)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE yard_states
            SET max_capacity = @maxCapacity,
                updated_at = NOW(),
                version = version + 1
            WHERE account_id = @accountId;
            """;
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("maxCapacity", maxCapacity);
        await command.ExecuteNonQueryAsync();
    }
}
