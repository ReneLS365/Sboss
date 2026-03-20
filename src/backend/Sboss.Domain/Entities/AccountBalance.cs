namespace Sboss.Domain.Entities;

public sealed class AccountBalance
{
    private AccountBalance(
        Guid accountId,
        string currencyCode,
        long balance,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        AccountId = accountId;
        CurrencyCode = currencyCode;
        Balance = balance;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid AccountId { get; }
    public string CurrencyCode { get; }
    public long Balance { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }

    public static AccountBalance Rehydrate(
        Guid accountId,
        string currencyCode,
        long balance,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
        ValidateTimestamps(createdAt, updatedAt);

        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Balance cannot be negative.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new AccountBalance(accountId, normalizedCurrencyCode, balance, createdAt, updatedAt, version);
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

    private static void ValidateTimestamps(DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }
    }
}
