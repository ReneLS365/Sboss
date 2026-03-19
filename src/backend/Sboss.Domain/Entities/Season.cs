namespace Sboss.Domain.Entities;

public sealed class Season
{
    private Season(
        Guid seasonId,
        string name,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        SeasonId = seasonId;
        Name = name;
        StartsAt = startsAt;
        EndsAt = endsAt;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid SeasonId { get; }
    public string Name { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static Season Create(
        Guid seasonId,
        string name,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isActive,
        DateTimeOffset createdAt)
    {
        return Rehydrate(seasonId, name, startsAt, endsAt, isActive, createdAt, createdAt, 1);
    }

    public static Season Rehydrate(
        Guid seasonId,
        string name,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (seasonId == Guid.Empty)
        {
            throw new ArgumentException("Season ID is required.", nameof(seasonId));
        }

        var normalizedName = NormalizeName(name);
        ValidateRange(startsAt, endsAt);
        ValidateTimestamps(createdAt, updatedAt);

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new Season(seasonId, normalizedName, startsAt, endsAt, isActive, createdAt, updatedAt, version);
    }

    public void UpdateSchedule(string name, DateTimeOffset startsAt, DateTimeOffset endsAt, bool isActive, DateTimeOffset updatedAt)
    {
        Name = NormalizeName(name);
        ValidateRange(startsAt, endsAt);
        ValidateTimestamps(CreatedAt, updatedAt);

        StartsAt = startsAt;
        EndsAt = endsAt;
        IsActive = isActive;
        UpdatedAt = updatedAt;
        Version++;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Season name is required.", nameof(name));
        }

        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Season name must be 128 characters or fewer.");
        }

        return normalized;
    }

    private static void ValidateRange(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (startsAt >= endsAt)
        {
            throw new ArgumentException("Season start time must be earlier than end time.");
        }
    }

    private static void ValidateTimestamps(DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }
    }
}
