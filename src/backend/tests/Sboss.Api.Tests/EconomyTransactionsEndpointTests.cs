using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Sboss.Contracts.Economy;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class EconomyTransactionsEndpointTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly PostgresDatabaseFixture _database;

    public EconomyTransactionsEndpointTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task CreditAndDebit_MutateAuthoritativeBalanceAndLedger()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var creditResponse = await client.PostAsJsonAsync("/api/v1/economy/transactions",
            new PostEconomyTransactionRequest(AccountId, "coin", 25, "credit-001", "contract_reward"));
        var debitResponse = await client.PostAsJsonAsync("/api/v1/economy/transactions",
            new PostEconomyTransactionRequest(AccountId, "COIN", -40, "debit-001", "shop_purchase"));

        Assert.Equal(HttpStatusCode.OK, creditResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, debitResponse.StatusCode);

        var creditBody = await creditResponse.Content.ReadFromJsonAsync<PostEconomyTransactionResponse>();
        var debitBody = await debitResponse.Content.ReadFromJsonAsync<PostEconomyTransactionResponse>();

        Assert.NotNull(creditBody);
        Assert.NotNull(debitBody);
        Assert.Equal("applied", creditBody!.Outcome);
        Assert.Equal(125, creditBody.ResultingBalance);
        Assert.Equal("applied", debitBody!.Outcome);
        Assert.Equal(85, debitBody.ResultingBalance);

        var state = await ReadEconomyStateAsync(AccountId, "COIN");
        Assert.Equal(85, state.Balance);
        Assert.Equal(3, state.LedgerCount);
        Assert.Equal(85, state.LastResultingBalance);
        Assert.Equal(85, state.LedgerSum);
    }

    [Fact]
    public async Task DuplicateRetry_ReturnsIdempotentResultWithoutDoubleApply()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new PostEconomyTransactionRequest(AccountId, "COIN", 15, "duplicate-001", "contract_reward");

        var firstResponse = await client.PostAsJsonAsync("/api/v1/economy/transactions", request);
        var secondResponse = await client.PostAsJsonAsync("/api/v1/economy/transactions", request);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<PostEconomyTransactionResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<PostEconomyTransactionResponse>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal("applied", firstBody!.Outcome);
        Assert.Equal("idempotent_replay", secondBody!.Outcome);
        Assert.Equal(firstBody.EconomyTransactionId, secondBody.EconomyTransactionId);
        Assert.Equal(firstBody.ResultingBalance, secondBody.ResultingBalance);

        var state = await ReadEconomyStateAsync(AccountId, "COIN");
        Assert.Equal(115, state.Balance);
        Assert.Equal(2, state.LedgerCount);
        Assert.Equal(115, state.LedgerSum);
    }

    [Fact]
    public async Task ConcurrentDuplicateAttempts_DoNotDuplicateCurrency()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new PostEconomyTransactionRequest(AccountId, "COIN", 30, "concurrent-001", "contract_reward");

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/economy/transactions", request),
            client.PostAsJsonAsync("/api/v1/economy/transactions", request),
            client.PostAsJsonAsync("/api/v1/economy/transactions", request),
            client.PostAsJsonAsync("/api/v1/economy/transactions", request));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<PostEconomyTransactionResponse>()));
        Assert.All(bodies, body => Assert.NotNull(body));
        Assert.Single(bodies.Select(body => body!.EconomyTransactionId).Distinct());
        Assert.Single(bodies.Select(body => body!.ResultingBalance).Distinct());
        Assert.Contains(bodies, body => body!.Outcome == "applied");
        Assert.Equal(3, bodies.Count(body => body!.Outcome == "idempotent_replay"));

        var state = await ReadEconomyStateAsync(AccountId, "COIN");
        Assert.Equal(130, state.Balance);
        Assert.Equal(2, state.LedgerCount);
        Assert.Equal(130, state.LastResultingBalance);
        Assert.Equal(130, state.LedgerSum);
    }

    [Fact]
    public async Task InsufficientFunds_IsRejectedWithoutPartialWrites()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/economy/transactions",
            new PostEconomyTransactionRequest(AccountId, "COIN", -150, "insufficient-001", "shop_purchase"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var state = await ReadEconomyStateAsync(AccountId, "COIN");
        Assert.Equal(100, state.Balance);
        Assert.Equal(1, state.LedgerCount);
        Assert.Equal(100, state.LedgerSum);
    }

    [Fact]
    public async Task UnknownAccount_IsRejectedWithoutCreatingBalanceOrLedger()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var missingAccountId = Guid.Parse("abababab-abab-abab-abab-abababababab");

        var response = await client.PostAsJsonAsync("/api/v1/economy/transactions",
            new PostEconomyTransactionRequest(missingAccountId, "COIN", 50, "unknown-001", "contract_reward"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var state = await ReadEconomyStateAsync(missingAccountId, "COIN");
        Assert.Equal(0, state.BalanceRowCount);
        Assert.Equal(0, state.LedgerCount);
    }

    private async Task<EconomyStateSnapshot> ReadEconomyStateAsync(Guid accountId, string currencyCode)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                COALESCE((
                    SELECT balance
                    FROM account_balances
                    WHERE account_id = @accountId AND currency_code = @currencyCode
                ), 0) AS balance,
                (
                    SELECT COUNT(*)
                    FROM account_balances
                    WHERE account_id = @accountId AND currency_code = @currencyCode
                ) AS balance_row_count,
                (
                    SELECT COUNT(*)
                    FROM economy_transactions
                    WHERE account_id = @accountId AND currency_code = @currencyCode
                ) AS ledger_count,
                COALESCE((
                    SELECT SUM(amount_delta)
                    FROM economy_transactions
                    WHERE account_id = @accountId AND currency_code = @currencyCode
                ), 0) AS ledger_sum,
                COALESCE((
                    SELECT resulting_balance
                    FROM economy_transactions
                    WHERE account_id = @accountId AND currency_code = @currencyCode
                    ORDER BY created_at DESC, economy_transaction_id DESC
                    LIMIT 1
                ), 0) AS last_resulting_balance;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("currencyCode", currencyCode);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new EconomyStateSnapshot(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4));
    }

    private sealed record EconomyStateSnapshot(
        long Balance,
        long BalanceRowCount,
        long LedgerCount,
        long LedgerSum,
        long LastResultingBalance);
}
