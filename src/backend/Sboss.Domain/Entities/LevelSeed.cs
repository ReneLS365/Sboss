namespace Sboss.Domain.Entities;

public sealed class LevelSeed
{
    public Guid LevelSeedId { get; set; }
    public string SeedValue { get; set; } = string.Empty;
    public string Biome { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string ModifiersJson { get; set; } = "{}";
    public int ParTimeMs { get; set; }
    public int GoldTimeMs { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
