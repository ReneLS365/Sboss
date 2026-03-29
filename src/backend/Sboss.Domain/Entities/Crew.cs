namespace Sboss.Domain.Entities;

public sealed class Crew
{
    private Crew(
        Guid crewId,
        Guid ownerAccountId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        CrewId = crewId;
        OwnerAccountId = ownerAccountId;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid CrewId { get; }
    public Guid OwnerAccountId { get; }
    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }

    public static Crew Create(Guid ownerAccountId, string name, DateTimeOffset createdAt)
    {
        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("Owner account ID is required.", nameof(ownerAccountId));
        }

        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("Crew name is required.", nameof(name));
        }

        if (normalizedName.Length > 64)
        {
            throw new ArgumentException("Crew name must be 64 characters or fewer.", nameof(name));
        }

        return new Crew(Guid.NewGuid(), ownerAccountId, normalizedName, createdAt, createdAt, 1);
    }

    public static Crew Rehydrate(
        Guid crewId,
        Guid ownerAccountId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (crewId == Guid.Empty)
        {
            throw new ArgumentException("Crew ID is required.", nameof(crewId));
        }

        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("Owner account ID is required.", nameof(ownerAccountId));
        }

        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length == 0 || normalizedName.Length > 64)
        {
            throw new ArgumentException("Crew name must be between 1 and 64 characters.", nameof(name));
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new Crew(crewId, ownerAccountId, normalizedName, createdAt, updatedAt, version);
    }
}
