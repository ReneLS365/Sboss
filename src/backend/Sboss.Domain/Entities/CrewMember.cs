namespace Sboss.Domain.Entities;

public sealed class CrewMember
{
    private CrewMember(
        Guid crewId,
        Guid accountId,
        CrewRole role,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        CrewId = crewId;
        AccountId = accountId;
        Role = role;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid CrewId { get; }
    public Guid AccountId { get; }
    public CrewRole Role { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }

    public static CrewMember Create(Guid crewId, Guid accountId, CrewRole role, DateTimeOffset createdAt)
    {
        if (crewId == Guid.Empty)
        {
            throw new ArgumentException("Crew ID is required.", nameof(crewId));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Crew role is invalid.");
        }

        return new CrewMember(crewId, accountId, role, createdAt, createdAt, 1);
    }

    public static CrewMember Rehydrate(
        Guid crewId,
        Guid accountId,
        CrewRole role,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (crewId == Guid.Empty)
        {
            throw new ArgumentException("Crew ID is required.", nameof(crewId));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Crew role is invalid.");
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new CrewMember(crewId, accountId, role, createdAt, updatedAt, version);
    }
}
