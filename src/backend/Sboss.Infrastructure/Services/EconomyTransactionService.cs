using System.Data;
using System.Linq;
using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed class EconomyTransactionService : IEconomyTransactionService
{
    private const string UniqueViolationSqlState = "23505";
    private readonly NpgsqlDataSource _dataSource;

    public EconomyTransactionService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<EconomyTransactionResult> ApplyAsync(EconomyMutationRequest request, CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var result = await ApplyInTransactionAsync(
                connection,
                transaction,
                normalizedRequest,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolationSqlState)
        {
            await transaction.RollbackAsync(cancellationToken);

            await using var replayConnection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var replayTransaction = await replayConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            var replayTransactionRecord = await GetTransactionByIdempotencyKeyAsync(
                replayConnection,
                replayTransaction,
                normalizedRequest.AccountId,
                normalizedRequest.IdempotencyKey,
                cancellationToken);

            if (replayTransactionRecord is null)
            {
                throw;
            }

            EnsureReplayIntentMatches(normalizedRequest, replayTransactionRecord);
            await replayTransaction.CommitAsync(cancellationToken);
            return new EconomyTransactionResult(replayTransactionRecord, CreateReplayBalanceSnapshot(replayTransactionRecord), true);
        }
    }

    public async Task<IReadOnlyList<EconomyTransactionResult>> ApplyBatchAsync(
        IReadOnlyList<EconomyMutationRequest> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, "At least one request is required.");
        }

        var normalizedRequests = requests.Select(NormalizeRequest).ToArray();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var results = new List<EconomyTransactionResult>(normalizedRequests.Length);
            foreach (var normalizedRequest in normalizedRequests)
            {
                var result = await ApplyInTransactionAsync(connection, transaction, normalizedRequest, cancellationToken);
                results.Add(result);
            }

            await transaction.CommitAsync(cancellationToken);
            return results;
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolationSqlState)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolveBatchReplayResultsAsync(normalizedRequests, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<EconomyTransactionResult>> ResolveBatchReplayResultsAsync(
        IReadOnlyList<EconomyMutationRequest> normalizedRequests,
        CancellationToken cancellationToken)
    {
        await using var replayConnection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var replayTransaction = await replayConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var replayResults = new List<EconomyTransactionResult>(normalizedRequests.Count);
        foreach (var normalizedRequest in normalizedRequests)
        {
            var replayTransactionRecord = await GetTransactionByIdempotencyKeyAsync(
                replayConnection,
                replayTransaction,
                normalizedRequest.AccountId,
                normalizedRequest.IdempotencyKey,
                cancellationToken);

            if (replayTransactionRecord is null)
            {
                throw new EconomyTransactionServiceException(
                    EconomyTransactionFailureReason.Conflict,
                    "Batch idempotency replay could not be resolved because at least one mutation record is missing.");
            }

            EnsureReplayIntentMatches(normalizedRequest, replayTransactionRecord);
            replayResults.Add(new EconomyTransactionResult(replayTransactionRecord, CreateReplayBalanceSnapshot(replayTransactionRecord), true));
        }

        await replayTransaction.CommitAsync(cancellationToken);
        return replayResults;
    }

    private static EconomyMutationRequest NormalizeRequest(EconomyMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AccountId == Guid.Empty)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, "Account ID is required.");
        }

        var currencyCode = NormalizeCurrencyCode(request.CurrencyCode);
        var idempotencyKey = NormalizeTrimmedValue(request.IdempotencyKey, nameof(request.IdempotencyKey), "Idempotency key");
        var reason = NormalizeTrimmedValue(request.Reason, nameof(request.Reason), "Reason");

        if (request.AmountDelta == 0)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, "Amount delta must not be zero.");
        }

        return request with
        {
            CurrencyCode = currencyCode,
            IdempotencyKey = idempotencyKey,
            Reason = reason
        };
    }

    private static async Task<EconomyTransactionResult> ApplyInTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EconomyMutationRequest normalizedRequest,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var existingTransaction = await GetTransactionByIdempotencyKeyAsync(
            connection,
            transaction,
            normalizedRequest.AccountId,
            normalizedRequest.IdempotencyKey,
            cancellationToken);

        if (existingTransaction is not null)
        {
            EnsureReplayIntentMatches(normalizedRequest, existingTransaction);
            return new EconomyTransactionResult(existingTransaction, CreateReplayBalanceSnapshot(existingTransaction), true);
        }

        var accountExists = await AccountExistsAsync(connection, transaction, normalizedRequest.AccountId, cancellationToken);
        if (!accountExists)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.UnknownAccount, "Account does not exist.");
        }

        await EnsureBalanceRowAsync(connection, transaction, normalizedRequest.AccountId, normalizedRequest.CurrencyCode, timestamp, cancellationToken);

        var lockedBalance = await GetBalanceAsync(
            connection,
            transaction,
            normalizedRequest.AccountId,
            normalizedRequest.CurrencyCode,
            true,
            cancellationToken);

        if (lockedBalance is null)
        {
            throw new InvalidOperationException("Authoritative balance row was not available after creation.");
        }

        var resultingBalance = checked(lockedBalance.Balance + normalizedRequest.AmountDelta);
        if (resultingBalance < 0)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InsufficientFunds, "Insufficient funds.");
        }

        var updatedBalance = await UpdateBalanceAsync(
            connection,
            transaction,
            lockedBalance,
            resultingBalance,
            timestamp,
            cancellationToken);

        var savedTransaction = await InsertTransactionAsync(
            connection,
            transaction,
            normalizedRequest,
            updatedBalance,
            timestamp,
            cancellationToken);

        return new EconomyTransactionResult(savedTransaction, updatedBalance, false);
    }

    private static string NormalizeCurrencyCode(string currencyCode)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);

        var normalized = currencyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, "Currency code is required.");
        }

        if (normalized.Length > 32)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, "Currency code must be 32 characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeTrimmedValue(string value, string paramName, string label)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, $"{label} is required.");
        }

        if (normalized.Length > 128)
        {
            throw new EconomyTransactionServiceException(EconomyTransactionFailureReason.InvalidRequest, $"{label} must be 128 characters or fewer.");
        }

        return normalized;
    }

    private static async Task<bool> AccountExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM accounts
            WHERE account_id = @accountId
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task EnsureBalanceRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string currencyCode,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO account_balances (account_id, currency_code, balance, created_at, updated_at, version)
            VALUES (@accountId, @currencyCode, 0, @createdAt, @updatedAt, 1)
            ON CONFLICT (account_id, currency_code) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("currencyCode", currencyCode);
        command.Parameters.AddWithValue("createdAt", timestamp);
        command.Parameters.AddWithValue("updatedAt", timestamp);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AccountBalance?> GetBalanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string currencyCode,
        bool lockForUpdate,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT account_id, currency_code, balance, created_at, updated_at, version
            FROM account_balances
            WHERE account_id = @accountId AND currency_code = @currencyCode
            """;

        if (lockForUpdate)
        {
            sql += Environment.NewLine + "FOR UPDATE";
        }

        sql += ";";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("currencyCode", currencyCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return AccountBalance.Rehydrate(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetInt64(5));
    }

    private static async Task<AccountBalance> UpdateBalanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AccountBalance currentBalance,
        long resultingBalance,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE account_balances
            SET balance = @balance,
                updated_at = @updatedAt,
                version = version + 1
            WHERE account_id = @accountId AND currency_code = @currencyCode
            RETURNING account_id, currency_code, balance, created_at, updated_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("balance", resultingBalance);
        command.Parameters.AddWithValue("updatedAt", timestamp);
        command.Parameters.AddWithValue("accountId", currentBalance.AccountId);
        command.Parameters.AddWithValue("currencyCode", currentBalance.CurrencyCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to update authoritative balance.");
        }

        return AccountBalance.Rehydrate(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetInt64(5));
    }

    private static async Task<EconomyTransaction> InsertTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EconomyMutationRequest request,
        AccountBalance updatedBalance,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO economy_transactions (
                economy_transaction_id,
                account_id,
                currency_code,
                idempotency_key,
                amount_delta,
                resulting_balance,
                resulting_balance_version,
                reason,
                created_at,
                version)
            VALUES (
                @economyTransactionId,
                @accountId,
                @currencyCode,
                @idempotencyKey,
                @amountDelta,
                @resultingBalance,
                @resultingBalanceVersion,
                @reason,
                @createdAt,
                1)
            RETURNING economy_transaction_id, account_id, currency_code, idempotency_key, amount_delta, resulting_balance, resulting_balance_version, reason, created_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("economyTransactionId", Guid.NewGuid());
        command.Parameters.AddWithValue("accountId", request.AccountId);
        command.Parameters.AddWithValue("currencyCode", request.CurrencyCode);
        command.Parameters.AddWithValue("idempotencyKey", request.IdempotencyKey);
        command.Parameters.AddWithValue("amountDelta", request.AmountDelta);
        command.Parameters.AddWithValue("resultingBalance", updatedBalance.Balance);
        command.Parameters.AddWithValue("resultingBalanceVersion", updatedBalance.Version);
        command.Parameters.AddWithValue("reason", request.Reason);
        command.Parameters.AddWithValue("createdAt", timestamp);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to persist economy transaction.");
        }

        return MapTransaction(reader);
    }

    private static async Task<EconomyTransaction?> GetTransactionByIdempotencyKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT economy_transaction_id, account_id, currency_code, idempotency_key, amount_delta, resulting_balance, resulting_balance_version, reason, created_at, version
            FROM economy_transactions
            WHERE account_id = @accountId AND idempotency_key = @idempotencyKey
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapTransaction(reader);
    }

    private static EconomyTransaction MapTransaction(NpgsqlDataReader reader)
    {
        return EconomyTransaction.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetString(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetInt64(9));
    }

    private static AccountBalance CreateReplayBalanceSnapshot(EconomyTransaction transaction)
    {
        return AccountBalance.Rehydrate(
            transaction.AccountId,
            transaction.CurrencyCode,
            transaction.ResultingBalance,
            transaction.CreatedAt,
            transaction.CreatedAt,
            transaction.ResultingBalanceVersion);
    }

    private static void EnsureReplayIntentMatches(EconomyMutationRequest request, EconomyTransaction replayTransaction)
    {
        if (!string.Equals(request.CurrencyCode, replayTransaction.CurrencyCode, StringComparison.Ordinal) ||
            request.AmountDelta != replayTransaction.AmountDelta ||
            !string.Equals(request.Reason, replayTransaction.Reason, StringComparison.Ordinal))
        {
            throw new EconomyTransactionServiceException(
                EconomyTransactionFailureReason.Conflict,
                "Idempotency key is already bound to a different economy transaction intent.");
        }
    }
}
