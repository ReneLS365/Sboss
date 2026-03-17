namespace Sboss.Domain.Entities;

public sealed class CosmeticUnlock
{
    public Guid CosmeticUnlockId { get; set; }
    public Guid AccountId { get; set; }
    public string CosmeticCode { get; set; } = string.Empty;
    public DateTimeOffset UnlockedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}
