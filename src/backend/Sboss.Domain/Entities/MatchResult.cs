namespace Sboss.Domain.Entities;

public sealed class MatchResult
{
    public Guid MatchResultId { get; set; }
    public Guid AccountId { get; set; }
    public Guid SeasonId { get; set; }
    public Guid LevelSeedId { get; set; }
    public int Score { get; set; }
    public int ClearTimeMs { get; set; }
    public int ComboMax { get; set; }
    public int Penalties { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}
