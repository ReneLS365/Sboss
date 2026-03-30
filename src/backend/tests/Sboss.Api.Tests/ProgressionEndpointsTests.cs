using System.Net;
using System.Net.Http.Json;
using Sboss.Contracts.Loadout;
using Sboss.Contracts.Progression;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ProgressionEndpointsTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherAccountId = Guid.Parse("f0f0f0f0-f0f0-f0f0-f0f0-f0f0f0f0f0f0");
    private static readonly Guid AcceptedMatchResultId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private static readonly Guid SeedOffshore = Guid.Parse("edededed-eded-eded-eded-edededededed");

    private readonly TestWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ProgressionEndpointsTests(PostgresDatabaseFixture database)
    {
        _database = database;
        _factory = new TestWebApplicationFactory(database.ConnectionString);
    }

    [Fact]
    public async Task AwardFromAcceptedMatch_AccumulatesXpAndLevelsUp()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var firstAward = await client.PostAsJsonAsync(
            "/api/v1/progression/awards",
            new PostProgressionAwardRequest(AccountId, AcceptedMatchResultId));

        Assert.Equal(HttpStatusCode.OK, firstAward.StatusCode);
        var firstBody = await firstAward.Content.ReadFromJsonAsync<PostProgressionAwardResponse>();
        Assert.NotNull(firstBody);
        Assert.Equal("awarded", firstBody!.Status);
        Assert.Equal(124, firstBody.XpAwarded);
        Assert.Equal(124, firstBody.TotalXp);
        Assert.Equal(1, firstBody.Level);
        Assert.False(firstBody.LeveledUp);

        var secondAward = await client.PostAsJsonAsync(
            "/api/v1/progression/awards",
            new PostProgressionAwardRequest(AccountId, AcceptedMatchResultId));

        Assert.Equal(HttpStatusCode.OK, secondAward.StatusCode);
        var secondBody = await secondAward.Content.ReadFromJsonAsync<PostProgressionAwardResponse>();
        Assert.NotNull(secondBody);
        Assert.Equal("idempotent_replay", secondBody!.Status);
        Assert.Equal(124, secondBody.TotalXp);
        Assert.Equal(1, secondBody.Level);
    }

    [Fact]
    public async Task AwardWithSpoofedAccount_IsRejected()
    {
        await _database.ResetAsync();
        await InsertSecondaryAccountAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/progression/awards",
            new PostProgressionAwardRequest(OtherAccountId, AcceptedMatchResultId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ProgressionReadAndTemplateUnlocks_RespectAuthoritativeLevel()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var lockedLoadoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/loadout/{AccountId}/{SeedOffshore}",
            new PostLoadoutSubmissionRequest(new[]
            {
                new LoadoutItemRequest("scaffold_blue_frame", 1),
                new LoadoutItemRequest("scaffold_yellow_deck", 1),
                new LoadoutItemRequest("scaffold_red_diagonal", 1)
            }));
        Assert.Equal(HttpStatusCode.Forbidden, lockedLoadoutResponse.StatusCode);

        for (var i = 0; i < 5; i++)
        {
            await InsertAcceptedMatchResultAsync($"33333333-3333-3333-3333-33333333333{i}");
            var latestMatchResultId = Guid.Parse($"33333333-3333-3333-3333-33333333333{i}");
            var award = await client.PostAsJsonAsync(
                "/api/v1/progression/awards",
                new PostProgressionAwardRequest(AccountId, latestMatchResultId));
            Assert.Equal(HttpStatusCode.OK, award.StatusCode);
        }

        var progression = await client.GetFromJsonAsync<GetProgressionStateResponse>($"/api/v1/progression/{AccountId}");
        Assert.NotNull(progression);
        Assert.True(progression!.TotalXp >= 500);
        Assert.True(progression.Level >= 3);
        Assert.Contains("template_offshore_rotation", progression.UnlockedTemplates);

        var unlockedLoadoutResponse = await client.PostAsJsonAsync(
            $"/api/v1/loadout/{AccountId}/{SeedOffshore}",
            new PostLoadoutSubmissionRequest(new[]
            {
                new LoadoutItemRequest("scaffold_blue_frame", 1),
                new LoadoutItemRequest("scaffold_yellow_deck", 1),
                new LoadoutItemRequest("scaffold_red_diagonal", 1)
            }));
        Assert.Equal(HttpStatusCode.OK, unlockedLoadoutResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentAwardsForDifferentMatchResults_DoNotLoseXp()
    {
        await _database.ResetAsync();
        await InsertAcceptedMatchResultAsync("44444444-4444-4444-4444-444444444440");
        var client = _factory.CreateClient();

        var tasks = new[]
        {
            client.PostAsJsonAsync("/api/v1/progression/awards", new PostProgressionAwardRequest(AccountId, AcceptedMatchResultId)),
            client.PostAsJsonAsync("/api/v1/progression/awards", new PostProgressionAwardRequest(AccountId, Guid.Parse("44444444-4444-4444-4444-444444444440")))
        };

        var responses = await Task.WhenAll(tasks);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var progression = await client.GetFromJsonAsync<GetProgressionStateResponse>($"/api/v1/progression/{AccountId}");
        Assert.NotNull(progression);
        Assert.Equal(248, progression!.TotalXp);
        Assert.Equal(2, progression.Level);
    }

    [Fact]
    public async Task ConcurrentDuplicateAwards_AreIdempotentWithoutServerError()
    {
        await _database.ResetAsync();
        var client = _factory.CreateClient();

        var duplicateRequests = Enumerable
            .Range(0, 8)
            .Select(_ => client.PostAsJsonAsync(
                "/api/v1/progression/awards",
                new PostProgressionAwardRequest(AccountId, AcceptedMatchResultId)))
            .ToArray();

        var responses = await Task.WhenAll(duplicateRequests);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var payloads = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<PostProgressionAwardResponse>()));
        Assert.Contains(payloads, payload => payload is not null && payload.Status == "awarded");
        Assert.Equal(1, payloads.Count(payload => payload is not null && payload.Status == "awarded"));
        Assert.All(payloads.Where(payload => payload is not null), payload => Assert.Equal(124, payload!.TotalXp));

        var progression = await client.GetFromJsonAsync<GetProgressionStateResponse>($"/api/v1/progression/{AccountId}");
        Assert.NotNull(progression);
        Assert.Equal(124, progression!.TotalXp);
    }

    private async Task InsertAcceptedMatchResultAsync(string matchResultId)
    {
        var sql = $"""
            INSERT INTO match_results (
                match_result_id,
                account_id,
                season_id,
                level_seed_id,
                score,
                clear_time_ms,
                combo_max,
                penalties,
                validation_status,
                created_at,
                updated_at,
                version)
            VALUES (
                '{matchResultId}',
                'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                'cccccccc-cccc-cccc-cccc-cccccccccccc',
                'dddddddd-dddd-dddd-dddd-dddddddddddd',
                1000,
                100000,
                12,
                0,
                'accepted',
                '2026-01-02T00:00:00Z',
                '2026-01-02T00:00:00Z',
                1)
            ON CONFLICT (match_result_id) DO NOTHING;
            """;

        await using var connection = new Npgsql.NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertSecondaryAccountAsync()
    {
        const string sql = """
            INSERT INTO accounts (account_id, external_ref, created_at, updated_at, version)
            VALUES ('f0f0f0f0-f0f0-f0f0-f0f0-f0f0f0f0f0f0', 'phase3e-alt-account', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
            ON CONFLICT (account_id) DO NOTHING;
            """;

        await using var connection = new Npgsql.NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
