namespace Sboss.Domain.Entities;

public sealed class EconomyTransaction
{
    private EconomyTransaction(
        Guid economyTransactionId,
        Guid accountId,
        string currencyCode,
        string idempotencyKey,
        long amountDelta,
        long resultingBalance,
        long resultingBalanceVersion,
        string reason,
        DateTimeOffset createdAt,
        long version)
    {
        EconomyTransactionId = economyTransactionId;
        AccountId = accountId;
        CurrencyCode = currencyCode;
        IdempotencyKey = idempotencyKey;
        AmountDelta = amountDelta;
        ResultingBalance = resultingBalance;
        ResultingBalanceVersion = resultingBalanceVersion;
        Reason = reason;
        CreatedAt = createdAt;
        Version = version;
    }

    public Guid EconomyTransactionId { get; }
    public Guid AccountId { get; }
    public string CurrencyCode { get; }
    public string IdempotencyKey { get; }
    public long AmountDelta { get; }
    public long ResultingBalance { get; }
    public long ResultingBalanceVersion { get; }
    public string Reason { get; }
    public DateTimeOffset CreatedAt { get; }
    public long Version { get; }

    public static EconomyTransaction Rehydrate(
        Guid economyTransactionId,
        Guid accountId,
        string currencyCode,
        string idempotencyKey,
        long amountDelta,
        long resultingBalance,
        long resultingBalanceVersion,
        string reason,
        DateTimeOffset createdAt,
        long version)
    {
        if (economyTransactionId == Guid.Empty)
        {
            throw new ArgumentException("Economy transaction ID is required.", nameof(economyTransactionId));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        var normalizedReason = NormalizeReason(reason);

        if (amountDelta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountDelta), "Amount delta must not be zero.");
        }

        if (resultingBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultingBalance), "Resulting balance cannot be negative.");
        }

        if (resultingBalanceVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultingBalanceVersion), "Resulting balance version must be greater than zero.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new EconomyTransaction(
            economyTransactionId,
            accountId,
            normalizedCurrencyCode,
            normalizedIdempotencyKey,
            amountDelta,
            resultingBalance,
            resultingBalanceVersion,
            normalizedReason,
            createdAt,
            version);
    }

    private static string NormalizeCurrencyCode(string currencyCode)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);

        var normalized = currencyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        }

        if (normalized.Length > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(currencyCode), "Currency code must be 32 characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        var normalized = idempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(idempotencyKey), "Idempotency key must be 128 characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeReason(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var normalized = reason.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "Reason must be 128 characters or fewer.");
        }

        return normalized;
    }
}
