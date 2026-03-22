using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Sboss.Contracts.ContractJobApplications;
using Sboss.Domain.Entities;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ContractJobApplicationsEndpointTests
{
    private static readonly Guid OwnerAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ApplicantOneAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
    private static readonly Guid ApplicantTwoAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private readonly PostgresDatabaseFixture _database;

    public ContractJobApplicationsEndpointTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task SubmitApplication_OpenJob_Succeeds()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications",
            new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-open-001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(body);
        Assert.Equal(ApplicantOneAccountId, body!.ApplicantAccountId);
        Assert.Equal("Submitted", body.ApplicationStatus);
        Assert.Equal("applied", body.Outcome);
        Assert.Equal(1, body.Version);

        var state = await ReadApplicationSnapshotAsync(body.ApplicationId);
        Assert.Equal("Submitted", state.Status);
        Assert.Equal(1, state.Version);
        Assert.Equal(1, state.MutationCount);
    }

    [Fact]
    public async Task SubmitApplication_NonOpenJob_IsRejected()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Accepted, 3);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications",
            new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-non-open-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountApplicationsForJobAsync(contractJobId));
    }

    [Fact]
    public async Task SubmitApplication_DuplicateReplay_ReturnsReplayWithoutDuplicateRows()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-replay-001");

        var firstResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications", request);
        var secondResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications", request);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var first = await firstResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.ApplicationId, second!.ApplicationId);
        Assert.Equal("applied", first.Outcome);
        Assert.Equal("idempotent_replay", second.Outcome);
        Assert.Equal(1, await CountApplicationsForJobAsync(contractJobId));
        Assert.Equal(1, await CountApplicationMutationsForJobAsync(contractJobId));
    }

    [Fact]
    public async Task SubmitApplication_SameApplicantSecondActiveSubmission_IsRejected()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications",
            new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-active-001"));
        var secondResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications",
            new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-active-002"));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(1, await CountApplicationsForJobAsync(contractJobId));
    }

    [Fact]
    public async Task WithdrawApplication_FromSubmitted_Succeeds()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        var applicationId = await InsertApplicationAsync(contractJobId, ApplicantOneAccountId, ContractJobApplicationStatus.Submitted, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationId}/withdraw",
            new PostContractJobApplicationMutationRequest("withdraw-001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Withdrawn", body!.ApplicationStatus);
        Assert.Equal(2, body.Version);
    }

    [Fact]
    public async Task SubmitApplication_ReplayAfterLaterWithdraw_ReturnsOriginalSubmittedSnapshot()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var submitRequest = new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-later-withdraw-001");

        var firstSubmitResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications", submitRequest);
        var firstSubmitBody = await firstSubmitResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(firstSubmitBody);

        var withdrawResponse = await client.PostAsJsonAsync(
            $"/api/v1/contract-jobs/{contractJobId}/applications/{firstSubmitBody!.ApplicationId}/withdraw",
            new PostContractJobApplicationMutationRequest("withdraw-after-submit-001"));
        Assert.Equal(HttpStatusCode.OK, withdrawResponse.StatusCode);

        var replaySubmitResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications", submitRequest);
        Assert.Equal(HttpStatusCode.OK, replaySubmitResponse.StatusCode);
        var replaySubmitBody = await replaySubmitResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();

        Assert.NotNull(replaySubmitBody);
        Assert.Equal("idempotent_replay", replaySubmitBody!.Outcome);
        Assert.Equal(firstSubmitBody.ApplicationId, replaySubmitBody.ApplicationId);
        Assert.Equal("Submitted", replaySubmitBody.ApplicationStatus);
        Assert.Equal(1, replaySubmitBody.Version);

        var currentState = await ReadApplicationSnapshotAsync(firstSubmitBody.ApplicationId);
        Assert.Equal("Withdrawn", currentState.Status);
        Assert.Equal(2, currentState.Version);
    }

    [Fact]
    public async Task WithdrawApplication_FromNonSubmittedState_IsRejected()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        var applicationId = await InsertApplicationAsync(contractJobId, ApplicantOneAccountId, ContractJobApplicationStatus.Accepted, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationId}/withdraw",
            new PostContractJobApplicationMutationRequest("withdraw-invalid-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var state = await ReadApplicationSnapshotAsync(applicationId);
        Assert.Equal("Accepted", state.Status);
        Assert.Equal(2, state.Version);
    }

    [Fact]
    public async Task AcceptApplication_FromSubmitted_Succeeds_AndJobBecomesAccepted()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        var applicationId = await InsertApplicationAsync(contractJobId, ApplicantOneAccountId, ContractJobApplicationStatus.Submitted, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationId}/accept",
            new PostContractJobApplicationMutationRequest("accept-001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Accepted", body!.ApplicationStatus);
        Assert.Equal("Accepted", body.ResultingJobState);
        Assert.Equal(applicationId, body.AcceptedApplicationId);

        var jobState = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Accepted", jobState.CurrentState);
        Assert.Equal(3, jobState.Version);
        Assert.Equal(1, jobState.TransitionCount);
    }

    [Fact]
    public async Task AcceptApplication_RejectsCompetingSubmittedApplications()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        await InsertApplicantAccountAsync(ApplicantTwoAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        var acceptedApplicationId = await InsertApplicationAsync(contractJobId, ApplicantOneAccountId, ContractJobApplicationStatus.Submitted, 1);
        var competingApplicationId = await InsertApplicationAsync(contractJobId, ApplicantTwoAccountId, ContractJobApplicationStatus.Submitted, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{acceptedApplicationId}/accept",
            new PostContractJobApplicationMutationRequest("accept-compete-001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var winner = await ReadApplicationSnapshotAsync(acceptedApplicationId);
        var loser = await ReadApplicationSnapshotAsync(competingApplicationId);
        Assert.Equal("Accepted", winner.Status);
        Assert.Equal("Rejected", loser.Status);
    }

    [Fact]
    public async Task ConcurrentAccepts_OnSameJob_YieldExactlyOneWinner()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        await InsertApplicantAccountAsync(ApplicantTwoAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        var applicationOneId = await InsertApplicationAsync(contractJobId, ApplicantOneAccountId, ContractJobApplicationStatus.Submitted, 1);
        var applicationTwoId = await InsertApplicationAsync(contractJobId, ApplicantTwoAccountId, ContractJobApplicationStatus.Submitted, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationOneId}/accept", new PostContractJobApplicationMutationRequest("accept-concurrent-001")),
            client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationTwoId}/accept", new PostContractJobApplicationMutationRequest("accept-concurrent-002")));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await CountApplicationsByStatusAsync(contractJobId, "Accepted"));
        Assert.Equal(1, await CountApplicationsByStatusAsync(contractJobId, "Rejected"));
        var jobState = await ReadContractJobStateAsync(contractJobId);
        Assert.Equal("Accepted", jobState.CurrentState);
        Assert.Equal(1, jobState.TransitionCount);
    }

    [Fact]
    public async Task AcceptApplication_Replay_DoesNotDoubleApply()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        await InsertApplicantAccountAsync(ApplicantTwoAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        var applicationId = await InsertApplicationAsync(contractJobId, ApplicantOneAccountId, ContractJobApplicationStatus.Submitted, 1);
        await InsertApplicationAsync(contractJobId, ApplicantTwoAccountId, ContractJobApplicationStatus.Submitted, 1);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new PostContractJobApplicationMutationRequest("accept-replay-001");

        var firstResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationId}/accept", request);
        var secondResponse = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications/{applicationId}/accept", request);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal("applied", firstBody!.Outcome);
        Assert.Equal("idempotent_replay", secondBody!.Outcome);
        Assert.Equal(firstBody.ApplicationId, secondBody.ApplicationId);
        Assert.Equal(1, await CountApplicationMutationsForJobAsync(contractJobId));
        Assert.Equal(1, await CountContractJobTransitionsAsync(contractJobId));
    }

    [Fact]
    public async Task WithdrawApplication_CanReuseSubmitIdempotencyKeyWithoutBeingTreatedAsSubmitReplay()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Open, 2);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        const string sharedIdempotencyKey = "shared-mutation-key-001";

        var submitResponse = await client.PostAsJsonAsync(
            $"/api/v1/contract-jobs/{contractJobId}/applications",
            new PostContractJobApplicationRequest(ApplicantOneAccountId, sharedIdempotencyKey));
        var submitBody = await submitResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(submitBody);

        var withdrawResponse = await client.PostAsJsonAsync(
            $"/api/v1/contract-jobs/{contractJobId}/applications/{submitBody!.ApplicationId}/withdraw",
            new PostContractJobApplicationMutationRequest(sharedIdempotencyKey));

        Assert.Equal(HttpStatusCode.OK, withdrawResponse.StatusCode);
        var withdrawBody = await withdrawResponse.Content.ReadFromJsonAsync<PostContractJobApplicationResponse>();
        Assert.NotNull(withdrawBody);
        Assert.Equal("applied", withdrawBody!.Outcome);
        Assert.Equal("Withdrawn", withdrawBody.ApplicationStatus);
        Assert.Equal(2, withdrawBody.Version);
        Assert.Equal(2, await CountApplicationMutationsForJobAsync(contractJobId));
    }

    [Fact]
    public async Task SubmitApplication_AfterJobAlreadyAccepted_IsRejected()
    {
        await _database.ResetAsync();
        await InsertApplicantAccountAsync(ApplicantOneAccountId);
        var contractJobId = await InsertContractJobAsync(ContractJobState.Accepted, 3);
        using var factory = new TestWebApplicationFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/contract-jobs/{contractJobId}/applications",
            new PostContractJobApplicationRequest(ApplicantOneAccountId, "submit-after-accepted-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountApplicationsForJobAsync(contractJobId));
    }

    private async Task InsertApplicantAccountAsync(Guid accountId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO accounts (account_id, external_ref, created_at, updated_at, version)
            VALUES (@accountId, @externalRef, @createdAt, @updatedAt, 1)
            ON CONFLICT (account_id) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("externalRef", $"acct-{accountId:N}");
        command.Parameters.AddWithValue("createdAt", DateTimeOffset.Parse("2026-03-22T00:00:00Z"));
        command.Parameters.AddWithValue("updatedAt", DateTimeOffset.Parse("2026-03-22T00:00:00Z"));
        await command.ExecuteNonQueryAsync();
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
        command.Parameters.AddWithValue("accountId", OwnerAccountId);
        command.Parameters.AddWithValue("currentState", state.ToString());
        command.Parameters.AddWithValue("createdAt", createdAt);
        command.Parameters.AddWithValue("updatedAt", updatedAt);
        command.Parameters.AddWithValue("version", version);
        await command.ExecuteNonQueryAsync();
        return contractJobId;
    }

    private async Task<Guid> InsertApplicationAsync(Guid contractJobId, Guid applicantAccountId, ContractJobApplicationStatus status, long version)
    {
        var applicationId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-03-22T00:10:00Z");
        var updatedAt = createdAt.AddMinutes(version - 1);

        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO contract_job_applications (contract_job_application_id, contract_job_id, applicant_account_id, status, created_at, updated_at, version)
            VALUES (@applicationId, @contractJobId, @applicantAccountId, @status, @createdAt, @updatedAt, @version);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("applicationId", applicationId);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        command.Parameters.AddWithValue("applicantAccountId", applicantAccountId);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("createdAt", createdAt);
        command.Parameters.AddWithValue("updatedAt", updatedAt);
        command.Parameters.AddWithValue("version", version);
        await command.ExecuteNonQueryAsync();
        return applicationId;
    }

    private async Task<int> CountApplicationsForJobAsync(Guid contractJobId) => await ExecuteScalarIntAsync(
        "SELECT COUNT(*) FROM contract_job_applications WHERE contract_job_id = @contractJobId;",
        contractJobId);

    private async Task<int> CountApplicationMutationsForJobAsync(Guid contractJobId) => await ExecuteScalarIntAsync(
        "SELECT COUNT(*) FROM contract_job_application_mutations WHERE contract_job_id = @contractJobId;",
        contractJobId);

    private async Task<int> CountApplicationsByStatusAsync(Guid contractJobId, string status)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM contract_job_applications WHERE contract_job_id = @contractJobId AND status = @status;", connection);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        command.Parameters.AddWithValue("status", status);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountContractJobTransitionsAsync(Guid contractJobId) => await ExecuteScalarIntAsync(
        "SELECT COUNT(*) FROM contract_job_transitions WHERE contract_job_id = @contractJobId;",
        contractJobId);

    private async Task<int> ExecuteScalarIntAsync(string sql, Guid contractJobId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<ApplicationSnapshot> ReadApplicationSnapshotAsync(Guid applicationId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                status,
                version,
                (
                    SELECT COUNT(*)
                    FROM contract_job_application_mutations
                    WHERE contract_job_application_id = @applicationId
                ) AS mutation_count
            FROM contract_job_applications
            WHERE contract_job_application_id = @applicationId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("applicationId", applicationId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new ApplicationSnapshot(reader.GetString(0), reader.GetInt64(1), reader.GetInt32(2));
    }

    private async Task<ContractJobStateSnapshot> ReadContractJobStateAsync(Guid contractJobId)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                current_state,
                version,
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
        await reader.ReadAsync();
        return new ContractJobStateSnapshot(reader.GetString(0), reader.GetInt64(1), reader.GetInt32(2));
    }

    private sealed record ApplicationSnapshot(string Status, long Version, int MutationCount);
    private sealed record ContractJobStateSnapshot(string CurrentState, long Version, int TransitionCount);
}
