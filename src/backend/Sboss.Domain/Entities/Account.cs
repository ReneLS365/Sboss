namespace Sboss.Domain.Entities;

public sealed class Account
{
    private Account(Guid accountId, string externalRef, DateTimeOffset createdAt, DateTimeOffset updatedAt, long version)
    {
        AccountId = accountId;
        ExternalRef = externalRef;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid AccountId { get; }
    public string ExternalRef { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static Account Create(Guid accountId, string externalRef, DateTimeOffset createdAt)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        var normalizedExternalRef = NormalizeExternalRef(externalRef);

        return new Account(accountId, normalizedExternalRef, createdAt, createdAt, 1);
    }

    public static Account Rehydrate(Guid accountId, string externalRef, DateTimeOffset createdAt, DateTimeOffset updatedAt, long version)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        var normalizedExternalRef = NormalizeExternalRef(externalRef);
        ValidateTimestamps(createdAt, updatedAt);

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new Account(accountId, normalizedExternalRef, createdAt, updatedAt, version);
    }

    public void UpdateExternalRef(string externalRef, DateTimeOffset updatedAt)
    {
        var normalizedExternalRef = NormalizeExternalRef(externalRef);
        ValidateTimestamps(CreatedAt, updatedAt);

        ExternalRef = normalizedExternalRef;
        UpdatedAt = updatedAt;
        Version++;
    }

    private static string NormalizeExternalRef(string externalRef)
    {
        ArgumentNullException.ThrowIfNull(externalRef);

        var normalized = externalRef.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("External reference is required.", nameof(externalRef));
        }

        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(externalRef), "External reference must be 128 characters or fewer.");
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
