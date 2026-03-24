using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Sboss.Contracts.ContractJobs;
using Sboss.Domain.Entities;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ContractJobsEndpointTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly PostgresDatabaseFixture _database;

    public ContractJobsEndpointTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task ValidTransitionProgression_Succeeds()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Draft, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var openResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Open", "job-open-001"));
        var acceptedResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Accepted", "job-accept-001"));
        var inProgressResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("InProgress", "job-progress-001"));
        var completedResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Completed", "job-complete-001"));

        Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inProgressResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);

        var completedBody = await completedResponse.Content.ReadFromJsonAsync<PostContractJobTransitionResponse>();
        Assert.NotNull(completedBody);
        Assert.Equal("Completed", completedBody!.CurrentState);
        Assert.Equal("applied", completedBody.Outcome);
        Assert.Equal(5, completedBody.Version);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Completed", state.CurrentState);
        Assert.Equal(5, state.Version);
        Assert.Equal(4, state.TransitionCount);
    }

    [Fact]
    public async Task InvalidTransition_IsRejectedWithStableConflictCode()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Draft, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Completed", "job-invalid-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Draft", state.CurrentState);
        Assert.Equal(1, state.Version);
        Assert.Equal(0, state.TransitionCount);
    }

    [Fact]
    public async Task TerminalStates_RejectFurtherTransitions()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Completed, 5);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Failed", "job-terminal-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Completed", state.CurrentState);
        Assert.Equal(5, state.Version);
        Assert.Equal(0, state.TransitionCount);
    }

    [Fact]
    public async Task DuplicateReplay_DoesNotDoubleApplyMutation()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new PostContractJobTransitionRequest("Accepted", "job-duplicate-001");

        var firstResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions", request);
        var secondResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions", request);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<PostContractJobTransitionResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<PostContractJobTransitionResponse>();
        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal("applied", firstBody!.Outcome);
        Assert.Equal("idempotent_replay", secondBody!.Outcome);
        Assert.Equal(firstBody.CurrentState, secondBody.CurrentState);
        Assert.Equal(firstBody.Version, secondBody.Version);
        Assert.Equal(firstBody.UpdatedAt, secondBody.UpdatedAt);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Accepted", state.CurrentState);
        Assert.Equal(3, state.Version);
        Assert.Equal(1, state.TransitionCount);
    }

    [Fact]
    public async Task DuplicateKey_WithDifferentTargetState_IsRejectedWithoutSilentReplay()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        const string idempotencyKey = "job-target-drift-001";

        var firstResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Accepted", idempotencyKey));
        var driftResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Failed", idempotencyKey));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, driftResponse.StatusCode);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Accepted", state.CurrentState);
        Assert.Equal(3, state.Version);
        Assert.Equal(1, state.TransitionCount);
    }

    [Fact]
    public async Task ConcurrentDuplicateReplay_DoesNotDoubleApplyMutation()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new PostContractJobTransitionRequest("Accepted", "job-concurrent-duplicate-001");

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions", request),
            client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions", request),
            client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions", request));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<PostContractJobTransitionResponse>()));
        Assert.All(bodies, body => Assert.NotNull(body));
        Assert.Single(bodies.Select(body => body!.Version).Distinct());
        Assert.Single(bodies.Select(body => body!.UpdatedAt).Distinct());
        Assert.Equal(1, bodies.Count(body => body!.Outcome == "applied"));
        Assert.Equal(2, bodies.Count(body => body!.Outcome == "idempotent_replay"));

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Accepted", state.CurrentState);
        Assert.Equal(3, state.Version);
        Assert.Equal(1, state.TransitionCount);
    }

    [Fact]
    public async Task ConcurrentTransitionAttempts_DoNotCorruptFinalState()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.InProgress, 4);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var completeTask = client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Completed", "job-race-complete-001"));
        var failTask = client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("Failed", "job-race-fail-001"));

        var responses = await Task.WhenAll(completeTask, failTask);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Contains(state.CurrentState, new[] { "Completed", "Failed" });
        Assert.Equal(5, state.Version);
        Assert.Equal(1, state.TransitionCount);
    }

    [Fact]
    public async Task MalformedTransitionRequest_IsRejectedWithStableValidationCode()
    {
        await _database.ResetAsync();
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/transitions",
            new PostContractJobTransitionRequest("NotARealState", "job-bad-request-001"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var state = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Open", state.CurrentState);
        Assert.Equal(2, state.Version);
        Assert.Equal(0, state.TransitionCount);
    }

    private async Task<Guid> InsertContractJobAsync(ContractJobState state, long version)
    {
        var contractJobId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-03-22T00:00:00Z");
        var updatedAt = createdAt.AddMinutes(version - 1);

        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO contract_jobs (contract_job_id, owning_account_id, current_state, created_at, updated_at, version)
            VALUES (@contractJobId, @accountId, @currentState, @createdAt, @updatedAt, @version);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        command.Parameters.AddWithValue("accountId", AccountId);
        command.Parameters.AddWithValue("currentState", state.ToString());
        command.Parameters.AddWithValue("createdAt", createdAt);
        command.Parameters.AddWithValue("updatedAt", updatedAt);
        command.Parameters.AddWithValue("version", version);
        await command.ExecuteNonQueryAsync();

        return contractJobId;
    }

    private async Task<ContractJobStateSnapshot> ReadContractJobStateAsync(Guid contractJobId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                current_state,
                version,
                updated_at,
                (
                    SELECT COUNT(*)
                    FROM contract_job_transitions
                    WHERE contract_job_id = @contractJobId
                ) AS transition_count
            FROM contract_jobs
            WHERE contract_job_id = @contractJobId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("contractJobId", contractJobId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new ContractJobStateSnapshot(
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetInt64(3));
    }

    private sealed record ContractJobStateSnapshot(
        string CurrentState,
        long Version,
        DateTimeOffset UpdatedAt,
        long TransitionCount);
}
