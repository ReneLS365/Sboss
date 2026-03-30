using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Sboss.Contracts.Crews;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class CrewEndpointsTests
{
    private static readonly Guid OwnerAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SvendAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
    private static readonly Guid LaerlingAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private readonly PostgresDatabaseFixture _database;

    public CrewEndpointsTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task CreateAssignPreview_UsesDeterministicSplit()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        await InsertAccountAsync(LaerlingAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Alpha"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);

        var assignSvendResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));
        var assignLaerlingResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, LaerlingAccountId, "Laerling"));

        Assert.Equal(HttpStatusCode.OK, assignSvendResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assignLaerlingResponse.StatusCode);

        var previewResponse = await client.GetAsync($"/api/v1/crews/{createdCrew.CrewId}/split-preview?grossAmount=100");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<GetCrewSplitPreviewResponse>();
        Assert.NotNull(preview);

        Assert.Equal(60, preview!.CrewShareAmount);
        Assert.Equal(40, preview.CompanyShareAmount);
        Assert.Equal(2, preview.Members.Count);
        Assert.Equal(40, preview.Members.Single(member => member.AccountId == SvendAccountId).Amount);
        Assert.Equal(20, preview.Members.Single(member => member.AccountId == LaerlingAccountId).Amount);
    }

    [Fact]
    public async Task AssignMember_NonOwnerActor_IsForbidden()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Auth"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);

        var assignResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(SvendAccountId, SvendAccountId, "Svend"));

        Assert.Equal(HttpStatusCode.Forbidden, assignResponse.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_SelfRemoval_IsAuthorized()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        var crewId = await InsertCrewAsync();
        await InsertCrewMemberAsync(crewId, SvendAccountId, "Svend");
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var removeResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{crewId}/members/{SvendAccountId}/remove",
            new PostCrewMemberRemovalRequest(SvendAccountId));

        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        Assert.Equal(0, await CountCrewMembersAsync(crewId, SvendAccountId));
    }

    [Fact]
    public async Task Payout_CreditsMemberBalancesAndCompanyBalance()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        await InsertAccountAsync(LaerlingAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Pay"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, LaerlingAccountId, "Laerling"));

        var payoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", "crew-payout-001", "contract_settlement"));

        Assert.Equal(HttpStatusCode.OK, payoutResponse.StatusCode);
        Assert.Equal(140, await ReadBalanceAsync(OwnerAccountId));
        Assert.Equal(40, await ReadBalanceAsync(SvendAccountId));
        Assert.Equal(20, await ReadBalanceAsync(LaerlingAccountId));
    }

    [Fact]
    public async Task CreateCrew_InvalidInput_ReturnsValidationProblem()
    {
        await _database.ResetAsync();
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(Guid.Empty, " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("crew"));
    }

    [Fact]
    public async Task Payout_NullStringInputs_ReturnsValidationProblem()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Null"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));

        var payload = JsonSerializer.Serialize(new
        {
            actorAccountId = OwnerAccountId,
            grossAmount = 100,
            currencyCode = (string?)null,
            idempotencyKey = (string?)null,
            reason = (string?)null
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var payoutResponse = await client.PostAsync($"/api/v1/crews/{createdCrew.CrewId}/payouts", content);

        Assert.Equal(HttpStatusCode.BadRequest, payoutResponse.StatusCode);
        var problem = await payoutResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("crewPayout"));
    }

    [Fact]
    public async Task Payout_CurrencyCodeLongerThan32Chars_ReturnsValidationProblem()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Currency Validation"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));

        var longCurrencyCode = new string('c', 33);
        var payoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, longCurrencyCode, "crew-long-currency", "contract_settlement"));

        Assert.Equal(HttpStatusCode.BadRequest, payoutResponse.StatusCode);
        var problem = await payoutResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("crewPayout"));
    }

    [Fact]
    public async Task Payout_MaxLengthIdempotencyKeyThatOverflowsDerivedMutation_ReturnsValidationProblem()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Long Idempotency"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));

        var maxClientIdempotency = new string('k', 128);
        var payoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", maxClientIdempotency, "contract_settlement"));

        Assert.Equal(HttpStatusCode.BadRequest, payoutResponse.StatusCode);
        Assert.Equal(0, await ReadPayoutSettlementCountAsync(createdCrew.CrewId, maxClientIdempotency));
    }

    [Fact]
    public async Task Payout_MaxLengthReasonThatOverflowsDerivedMutation_ReturnsValidationProblem()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Long Reason"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));

        var maxClientReason = new string('r', 128);
        var payoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", "crew-long-reason", maxClientReason));

        Assert.Equal(HttpStatusCode.BadRequest, payoutResponse.StatusCode);
        Assert.Equal(0, await ReadPayoutSettlementCountAsync(createdCrew.CrewId, "crew-long-reason"));
    }

    [Fact]
    public async Task Payout_EmptyCrewId_ReturnsValidationProblem()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var payoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{Guid.Empty}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", "crew-empty-id", "contract_settlement"));

        Assert.Equal(HttpStatusCode.BadRequest, payoutResponse.StatusCode);
        var problem = await payoutResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("crewPayout"));
    }

    [Fact]
    public async Task Payout_ReplayStaysStableAfterMembershipChanges()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        await InsertAccountAsync(LaerlingAccountId);
        var replacementAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3");
        await InsertAccountAsync(replacementAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Replay Stable"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);

        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, LaerlingAccountId, "Laerling"));

        var firstPayout = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", "crew-replay-stable", "contract_settlement"));
        Assert.Equal(HttpStatusCode.OK, firstPayout.StatusCode);

        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members/{LaerlingAccountId}/remove",
            new PostCrewMemberRemovalRequest(OwnerAccountId));
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, replacementAccountId, "Laerling"));

        var replayPayout = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", "crew-replay-stable", "contract_settlement"));
        Assert.Equal(HttpStatusCode.OK, replayPayout.StatusCode);

        Assert.Equal(140, await ReadBalanceAsync(OwnerAccountId));
        Assert.Equal(40, await ReadBalanceAsync(SvendAccountId));
        Assert.Equal(20, await ReadBalanceAsync(LaerlingAccountId));
        Assert.Equal(0, await ReadBalanceAsync(replacementAccountId));
    }

    [Fact]
    public async Task Payout_ReplayWithCurrencyCaseDifference_RemainsIdempotent()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        await InsertAccountAsync(LaerlingAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Replay Currency Case"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, LaerlingAccountId, "Laerling"));

        var firstPayout = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "coin", "crew-replay-case", "contract_settlement"));
        Assert.Equal(HttpStatusCode.OK, firstPayout.StatusCode);

        var replayPayout = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 100, "COIN", "crew-replay-case", "contract_settlement"));
        Assert.Equal(HttpStatusCode.OK, replayPayout.StatusCode);

        Assert.Equal(140, await ReadBalanceAsync(OwnerAccountId));
        Assert.Equal(40, await ReadBalanceAsync(SvendAccountId));
        Assert.Equal(20, await ReadBalanceAsync(LaerlingAccountId));
    }

    [Fact]
    public async Task Payout_InvalidMutationValidation_DoesNotPersistSettlementSnapshot()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Invalid Mutation"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));

        var payoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/payouts",
            new PostCrewPayoutRequest(OwnerAccountId, 1, "COIN", "crew-invalid-mutation", "contract_settlement"));

        Assert.Equal(HttpStatusCode.BadRequest, payoutResponse.StatusCode);
        Assert.Equal(0, await ReadPayoutSettlementCountAsync(createdCrew.CrewId, "crew-invalid-mutation"));
    }

    [Fact]
    public async Task Payout_ConcurrentReplay_DoesNotReturnInternalServerError()
    {
        await _database.ResetAsync();
        await InsertAccountAsync(SvendAccountId);
        await InsertAccountAsync(LaerlingAccountId);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/crews", new PostCreateCrewRequest(OwnerAccountId, "Crew Race"));
        var createdCrew = await createResponse.Content.ReadFromJsonAsync<PostCreateCrewResponse>();
        Assert.NotNull(createdCrew);
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew!.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, SvendAccountId, "Svend"));
        await client.PostAsJsonAsync(
            $"/api/v1/crews/{createdCrew.CrewId}/members",
            new PostCrewMemberAssignmentRequest(OwnerAccountId, LaerlingAccountId, "Laerling"));

        for (var iteration = 0; iteration < 5; iteration++)
        {
            var request = new PostCrewPayoutRequest(
                OwnerAccountId,
                100,
                "COIN",
                $"crew-race-idempotency-{iteration}",
                "contract_settlement");
            var responses = await Task.WhenAll(
                client.PostAsJsonAsync($"/api/v1/crews/{createdCrew.CrewId}/payouts", request),
                client.PostAsJsonAsync($"/api/v1/crews/{createdCrew.CrewId}/payouts", request));

            foreach (var response in responses)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Assert.NotEqual(
                    HttpStatusCode.InternalServerError,
                    response.StatusCode);
                Assert.True(
                    response.StatusCode == HttpStatusCode.OK,
                    $"Expected HTTP 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");
            }
        }
    }

    private async Task InsertAccountAsync(Guid accountId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO accounts (account_id, external_ref, created_at, updated_at, version)
            VALUES (@accountId, @externalRef, NOW(), NOW(), 1)
            ON CONFLICT (account_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("externalRef", $"ext-{accountId:N}");
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertCrewAsync()
    {
        var crewId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO crews (crew_id, owner_account_id, name, created_at, updated_at, version)
            VALUES (@crewId, @ownerAccountId, 'Crew Removal', NOW(), NOW(), 1);
            """;
        command.Parameters.AddWithValue("crewId", crewId);
        command.Parameters.AddWithValue("ownerAccountId", OwnerAccountId);
        await command.ExecuteNonQueryAsync();
        return crewId;
    }

    private async Task InsertCrewMemberAsync(Guid crewId, Guid accountId, string role)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO crew_members (crew_id, account_id, role, created_at, updated_at, version)
            VALUES (@crewId, @accountId, @role, NOW(), NOW(), 1);
            """;
        command.Parameters.AddWithValue("crewId", crewId);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("role", role);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> CountCrewMembersAsync(Guid crewId, Guid accountId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM crew_members
            WHERE crew_id = @crewId AND account_id = @accountId;
            """;
        command.Parameters.AddWithValue("crewId", crewId);
        command.Parameters.AddWithValue("accountId", accountId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<long> ReadBalanceAsync(Guid accountId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(balance, 0)
            FROM account_balances
            WHERE account_id = @accountId AND currency_code = 'COIN';
            """;
        command.Parameters.AddWithValue("accountId", accountId);
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
    }

    private async Task<int> ReadPayoutSettlementCountAsync(Guid crewId, string idempotencyKey)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM crew_payout_settlements
            WHERE crew_id = @crewId AND idempotency_key = @idempotencyKey;
            """;
        command.Parameters.AddWithValue("crewId", crewId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
