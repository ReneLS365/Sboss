using System.Data;
using Npgsql;
using Sboss.Domain.Entities;

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
            var existingJob = await ContractJobStateMutationHelper.GetContractJobAsync(connection, transaction, normalizedRequest.ContractJobId, false, cancellationToken);
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

        var lockedJob = await ContractJobStateMutationHelper.GetContractJobAsync(connection, transaction, normalizedRequest.ContractJobId, true, cancellationToken);
        if (lockedJob is null)
        {
            throw new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.NotFound, "Contract job does not exist.");
        }

        var timestamp = DateTimeOffset.UtcNow;

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

        ContractJob savedJob;
        try
        {
            savedJob = await ContractJobStateMutationHelper.ApplyTransitionAsync(
                connection,
                transaction,
                lockedJob,
                normalizedRequest.TargetState,
                normalizedRequest.IdempotencyKey,
                timestamp,
                cancellationToken);
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

            var replayJob = await ContractJobStateMutationHelper.GetContractJobAsync(replayConnection, replayTransaction, normalizedRequest.ContractJobId, false, cancellationToken);
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
        catch (ContractJobTransitionServiceException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
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

        var idempotencyKey = ContractJobStateMutationHelper.NormalizeTrimmedValue(
            request.IdempotencyKey,
            nameof(request.IdempotencyKey),
            "Idempotency key",
            message => new ContractJobTransitionServiceException(ContractJobTransitionFailureReason.InvalidRequest, message));

        return request with { IdempotencyKey = idempotencyKey };
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
