using System.Data;
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
        var timestamp = DateTimeOffset.UtcNow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var existingTransaction = await GetTransactionByIdempotencyKeyAsync(
            connection,
            transaction,
            normalizedRequest.AccountId,
            normalizedRequest.IdempotencyKey,
            cancellationToken);

        if (existingTransaction is not null)
        {
            var existingBalance = await GetBalanceAsync(
                connection,
                transaction,
                existingTransaction.AccountId,
                existingTransaction.CurrencyCode,
                false,
                cancellationToken);

            if (existingBalance is null)
            {
                throw new InvalidOperationException("Existing idempotent economy transaction is missing its authoritative balance state.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new EconomyTransactionResult(existingTransaction, existingBalance, true);
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

        EconomyTransaction savedTransaction;
        try
        {
            savedTransaction = await InsertTransactionAsync(
                connection,
                transaction,
                normalizedRequest,
                resultingBalance,
                timestamp,
                cancellationToken);
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

            var replayBalance = await GetBalanceAsync(
                replayConnection,
                replayTransaction,
                replayTransactionRecord.AccountId,
                replayTransactionRecord.CurrencyCode,
                false,
                cancellationToken);

            if (replayBalance is null)
            {
                throw new InvalidOperationException("Existing idempotent economy transaction is missing its authoritative balance state.");
            }

            await replayTransaction.CommitAsync(cancellationToken);
            return new EconomyTransactionResult(replayTransactionRecord, replayBalance, true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new EconomyTransactionResult(savedTransaction, updatedBalance, false);
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
        long resultingBalance,
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
                @reason,
                @createdAt,
                1)
            RETURNING economy_transaction_id, account_id, currency_code, idempotency_key, amount_delta, resulting_balance, reason, created_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("economyTransactionId", Guid.NewGuid());
        command.Parameters.AddWithValue("accountId", request.AccountId);
        command.Parameters.AddWithValue("currencyCode", request.CurrencyCode);
        command.Parameters.AddWithValue("idempotencyKey", request.IdempotencyKey);
        command.Parameters.AddWithValue("amountDelta", request.AmountDelta);
        command.Parameters.AddWithValue("resultingBalance", resultingBalance);
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
            SELECT economy_transaction_id, account_id, currency_code, idempotency_key, amount_delta, resulting_balance, reason, created_at, version
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
            reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetInt64(8));
    }
}
