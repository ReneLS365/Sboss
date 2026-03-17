namespace Sboss.Domain.Entities;

public sealed class PlayerProfile
{
    public Guid PlayerProfileId { get; set; }
    public Guid AccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Experience { get; set; }
    public int Level { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}
