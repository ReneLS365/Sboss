namespace Sboss.Domain.Entities;

public sealed class Account
{
    public Guid AccountId { get; set; }
    public string ExternalRef { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}
