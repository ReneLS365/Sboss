using System.Data;
using Npgsql;
using Sboss.Domain.Entities;
using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure.Services;

public sealed class ContractJobTransitionService : IContractJobTransitionService
{
    private const string UniqueViolationSqlState = "23505";
    private readonly NpgsqlDataSource _dataSource;

    public ContractJobTransitionService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ContractJobTransitionResult> TransitionAsync(ContractJobTransitionRequest request, CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);
        var timestamp = DateTimeOffset.UtcNow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var existingTransition = await GetTransitionByIdempotencyKeyAsync(
            connection,
            transaction,
            normalizedRequest.ContractJobId,
            normalizedRequest.IdempotencyKey,
            cancellationToken);

        if (existingTransition is not null)
        {
            var existingJob = await GetContractJobAsync(connection, transaction, normalizedRequest.ContractJobId, false, cancellationToken);
            if (existingJob is null)
            {
                throw new InvalidOperationException("Contract job disappeared during replay lookup.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new ContractJobTransitionResult(
                ContractJob.Rehydrate(
                    existingJob.ContractJobId,
                    existingJob.OwningAccountId,
                    existingTransition.ToState,
                    existingJob.CreatedAt,
                    existingTransition.CreatedAt,
                    existingTransition.ResultingVersion),
                true);
        }

        var lockedJob = await GetContractJobAsync(connection, transaction, normalizedRequest.ContractJobId, true, cancellationToken);
        if (lockedJob is null)
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.NotFound, "Contract job does not exist.");
        }

        existingTransition = await GetTransitionByIdempotencyKeyAsync(
            connection,
            transaction,
            normalizedRequest.ContractJobId,
            normalizedRequest.IdempotencyKey,
            cancellationToken);

        if (existingTransition is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobTransitionResult(
                ContractJob.Rehydrate(
                    lockedJob.ContractJobId,
                    lockedJob.OwningAccountId,
                    existingTransition.ToState,
                    lockedJob.CreatedAt,
                    existingTransition.CreatedAt,
                    existingTransition.ResultingVersion),
                true);
        }

        ContractJob transitionedJob;
        try
        {
            transitionedJob = lockedJob.TransitionTo(normalizedRequest.TargetState, timestamp);
        }
        catch (InvalidOperationException exception)
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.InvalidTransition, exception.Message);
        }

        var savedJob = await UpdateContractJobAsync(
            connection,
            transaction,
            normalizedRequest.ContractJobId,
            lockedJob,
            transitionedJob,
            cancellationToken);

        try
        {
            await InsertTransitionAsync(connection, transaction, lockedJob, savedJob, normalizedRequest.IdempotencyKey, timestamp, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolationSqlState)
        {
            await transaction.RollbackAsync(cancellationToken);

            await using var replayConnection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var replayTransaction = await replayConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var replayTransition = await GetTransitionByIdempotencyKeyAsync(
                replayConnection,
                replayTransaction,
                normalizedRequest.ContractJobId,
                normalizedRequest.IdempotencyKey,
                cancellationToken);

            var replayJob = await GetContractJobAsync(replayConnection, replayTransaction, normalizedRequest.ContractJobId, false, cancellationToken);
            if (replayTransition is null || replayJob is null)
            {
                throw;
            }

            await replayTransaction.CommitAsync(cancellationToken);
            return new ContractJobTransitionResult(
                ContractJob.Rehydrate(
                    replayJob.ContractJobId,
                    replayJob.OwningAccountId,
                    replayTransition.ToState,
                    replayJob.CreatedAt,
                    replayTransition.CreatedAt,
                    replayTransition.ResultingVersion),
                true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ContractJobTransitionResult(savedJob, false);
    }

    private static ContractJobTransitionRequest NormalizeRequest(ContractJobTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ContractJobId == Guid.Empty)
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.InvalidRequest, "Contract job ID is required.");
        }

        if (!Enum.IsDefined(request.TargetState))
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.InvalidRequest, "Target state is invalid.");
        }

        var idempotencyKey = NormalizeTrimmedValue(request.IdempotencyKey, nameof(request.IdempotencyKey), "Idempotency key");
        return request with { IdempotencyKey = idempotencyKey };
    }

    private static string NormalizeTrimmedValue(string value, string paramName, string label)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.InvalidRequest, $"{label} is required.");
        }

        if (normalized.Length > 128)
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.InvalidRequest, $"{label} must be 128 characters or fewer.");
        }

        return normalized;
    }

    private static async Task<ContractJob?> GetContractJobAsync(
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

    private static async Task<ContractJob> UpdateContractJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid contractJobId,
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
            throw await CreateOptimisticConcurrencyExceptionAsync(
                connection,
                transaction,
                contractJobId,
                transitionedJob.CurrentState,
                cancellationToken);
        }

        return PostgresContractJobRepository.MapContractJob(reader);
    }

    private static async Task<ContractJobTransitionServiceException> CreateOptimisticConcurrencyExceptionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid contractJobId,
        ContractJobState requestedTargetState,
        CancellationToken cancellationToken)
    {
        var latestJob = await GetContractJobAsync(connection, transaction, contractJobId, false, cancellationToken);
        if (latestJob is null)
        {
            return new ContractJobTransitionServiceException(
                ContractJobTransitionFailureReason.NotFound,
                "Contract job does not exist.");
        }

        return new ContractJobTransitionServiceException(
            ContractJobTransitionFailureReason.InvalidTransition,
            $"Contract job transition from {latestJob.CurrentState} to {requestedTargetState} is not allowed.");
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

    private static async Task<ContractJobTransitionRecord?> GetTransitionByIdempotencyKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid contractJobId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT contract_job_transition_id, contract_job_id, idempotency_key, from_state, to_state, resulting_version, created_at
            FROM contract_job_transitions
            WHERE contract_job_id = @contractJobId AND idempotency_key = @idempotencyKey
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ContractJobTransitionRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            Enum.Parse<ContractJobState>(reader.GetString(3), ignoreCase: false),
            Enum.Parse<ContractJobState>(reader.GetString(4), ignoreCase: false),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6));
    }

    private sealed record ContractJobTransitionRecord(
        Guid ContractJobTransitionId,
        Guid ContractJobId,
        string IdempotencyKey,
        ContractJobState FromState,
        ContractJobState ToState,
        long ResultingVersion,
        DateTimeOffset CreatedAt);
}
