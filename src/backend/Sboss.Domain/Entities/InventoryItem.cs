namespace Sboss.Domain.Entities;

public sealed class InventoryItem
{
    public Guid InventoryItemId { get; set; }
    public Guid AccountId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}
