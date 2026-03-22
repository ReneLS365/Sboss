using Npgsql;
using Sboss.Domain.Entities;
using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure.Services;

internal static class ContractJobStateMutationHelper
{
    public static string NormalizeTrimmedValue(string value, string paramName, string label, Func<string, Exception> createException)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw createException($"{label} is required.");
        }

        if (normalized.Length > 128)
        {
            throw createException($"{label} must be 128 characters or fewer.");
        }

        return normalized;
    }

    public static async Task<ContractJob?> GetContractJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid contractJobId,
        bool lockForUpdate,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT contract_job_id, owning_account_id, current_state, created_at, updated_at, version
            FROM contract_jobs
            WHERE contract_job_id = @contractJobId
            """;

        if (lockForUpdate)
        {
            sql += Environment.NewLine + "FOR UPDATE";
        }

        sql += ";";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("contractJobId", contractJobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return PostgresContractJobRepository.MapContractJob(reader);
    }

    public static async Task<ContractJob> ApplyTransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ContractJob lockedJob,
        ContractJobState targetState,
        string idempotencyKey,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ContractJob transitionedJob;
        try
        {
            transitionedJob = lockedJob.TransitionTo(targetState, timestamp);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ContractJobTransitionServiceException(
                ContractJobTransitionFailureReason.InvalidTransition,
                $"Contract job transition from {lockedJob.CurrentState} to {targetState} is not allowed.");
        }

        var savedJob = await UpdateContractJobAsync(connection, transaction, lockedJob, transitionedJob, cancellationToken);
        await InsertTransitionAsync(connection, transaction, lockedJob, savedJob, idempotencyKey, timestamp, cancellationToken);
        return savedJob;
    }

    private static async Task<ContractJob> UpdateContractJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ContractJob currentJob,
        ContractJob transitionedJob,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE contract_jobs
            SET current_state = @currentState,
                updated_at = @updatedAt,
                version = @newVersion
            WHERE contract_job_id = @contractJobId
              AND version = @expectedVersion
            RETURNING contract_job_id, owning_account_id, current_state, created_at, updated_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("currentState", transitionedJob.CurrentState.ToString());
        command.Parameters.AddWithValue("updatedAt", transitionedJob.UpdatedAt);
        command.Parameters.AddWithValue("newVersion", transitionedJob.Version);
        command.Parameters.AddWithValue("contractJobId", currentJob.ContractJobId);
        command.Parameters.AddWithValue("expectedVersion", currentJob.Version);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            var latestJob = await GetContractJobAsync(connection, transaction, currentJob.ContractJobId, false, cancellationToken);
            if (latestJob is null)
            {
                throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.NotFound, "Contract job does not exist.");
            }

            throw new ContractJobTransitionServiceException(
                ContractJobTransitionFailureReason.InvalidTransition,
                $"Contract job transition from {latestJob.CurrentState} to {transitionedJob.CurrentState} is not allowed.");
        }

        return PostgresContractJobRepository.MapContractJob(reader);
    }

    private static async Task InsertTransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ContractJob currentJob,
        ContractJob transitionedJob,
        string idempotencyKey,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO contract_job_transitions (
                contract_job_transition_id,
                contract_job_id,
                idempotency_key,
                from_state,
                to_state,
                resulting_version,
                created_at)
            VALUES (
                @transitionId,
                @contractJobId,
                @idempotencyKey,
                @fromState,
                @toState,
                @resultingVersion,
                @createdAt);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("transitionId", Guid.NewGuid());
        command.Parameters.AddWithValue("contractJobId", currentJob.ContractJobId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("fromState", currentJob.CurrentState.ToString());
        command.Parameters.AddWithValue("toState", transitionedJob.CurrentState.ToString());
        command.Parameters.AddWithValue("resultingVersion", transitionedJob.Version);
        command.Parameters.AddWithValue("createdAt", timestamp);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
