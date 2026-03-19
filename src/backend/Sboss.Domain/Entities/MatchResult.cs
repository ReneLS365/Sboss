namespace Sboss.Domain.Entities;

public sealed class MatchResult
{
    private MatchResult(
        Guid matchResultId,
        Guid accountId,
        Guid seasonId,
        Guid levelSeedId,
        int score,
        int clearTimeMs,
        int comboMax,
        int penalties,
        MatchValidationStatus validationStatus,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        MatchResultId = matchResultId;
        AccountId = accountId;
        SeasonId = seasonId;
        LevelSeedId = levelSeedId;
        Score = score;
        ClearTimeMs = clearTimeMs;
        ComboMax = comboMax;
        Penalties = penalties;
        ValidationStatus = validationStatus;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid MatchResultId { get; }
    public Guid AccountId { get; }
    public Guid SeasonId { get; }
    public Guid LevelSeedId { get; }
    public int Score { get; }
    public int ClearTimeMs { get; }
    public int ComboMax { get; }
    public int Penalties { get; }
    public MatchValidationStatus ValidationStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static MatchResult Create(Guid accountId, Guid seasonId, Guid levelSeedId, int score, int clearTimeMs, int comboMax, int penalties, DateTimeOffset createdAt)
    {
        return new MatchResult(Guid.NewGuid(), accountId, seasonId, levelSeedId, score, clearTimeMs, comboMax, penalties, MatchValidationStatus.Pending, createdAt, createdAt, 1)
            .EnsureValid();
    }

    public static MatchResult Rehydrate(
        Guid matchResultId,
        Guid accountId,
        Guid seasonId,
        Guid levelSeedId,
        int score,
        int clearTimeMs,
        int comboMax,
        int penalties,
        MatchValidationStatus validationStatus,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (matchResultId == Guid.Empty)
        {
            throw new ArgumentException("Match result ID is required.", nameof(matchResultId));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }

        return new MatchResult(matchResultId, accountId, seasonId, levelSeedId, score, clearTimeMs, comboMax, penalties, validationStatus, createdAt, updatedAt, version)
            .EnsureValid();
    }

    public void ApplyValidation(MatchValidationStatus validationStatus, DateTimeOffset updatedAt)
    {
        if (updatedAt < CreatedAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.", nameof(updatedAt));
        }

        ValidationStatus = validationStatus;
        UpdatedAt = updatedAt;
        Version++;
    }

    private MatchResult EnsureValid()
    {
        if (AccountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(AccountId));
        }

        if (SeasonId == Guid.Empty)
        {
            throw new ArgumentException("Season ID is required.", nameof(SeasonId));
        }

        if (LevelSeedId == Guid.Empty)
        {
            throw new ArgumentException("Level seed ID is required.", nameof(LevelSeedId));
        }

        if (Score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Score), "Score cannot be negative.");
        }

        if (ClearTimeMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ClearTimeMs), "Clear time must be greater than zero.");
        }

        if (ComboMax < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ComboMax), "Combo max cannot be negative.");
        }

        if (Penalties < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Penalties), "Penalties cannot be negative.");
        }

        return this;
    }
}

public enum MatchValidationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}
