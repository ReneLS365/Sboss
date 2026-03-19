namespace Sboss.Domain.Entities;

public sealed class LevelSeed
{
    private LevelSeed(
        Guid levelSeedId,
        string seedValue,
        string biome,
        string template,
        string objective,
        string modifiersJson,
        int parTimeMs,
        int goldTimeMs,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        LevelSeedId = levelSeedId;
        SeedValue = seedValue;
        Biome = biome;
        Template = template;
        Objective = objective;
        ModifiersJson = modifiersJson;
        ParTimeMs = parTimeMs;
        GoldTimeMs = goldTimeMs;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid LevelSeedId { get; }
    public string SeedValue { get; private set; }
    public string Biome { get; private set; }
    public string Template { get; private set; }
    public string Objective { get; private set; }
    public string ModifiersJson { get; private set; }
    public int ParTimeMs { get; private set; }
    public int GoldTimeMs { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static LevelSeed Create(
        Guid levelSeedId,
        string seedValue,
        string biome,
        string template,
        string objective,
        string modifiersJson,
        int parTimeMs,
        int goldTimeMs,
        DateTimeOffset createdAt)
    {
        return Rehydrate(levelSeedId, seedValue, biome, template, objective, modifiersJson, parTimeMs, goldTimeMs, 1, createdAt, createdAt);
    }

    public static LevelSeed Rehydrate(
        Guid levelSeedId,
        string seedValue,
        string biome,
        string template,
        string objective,
        string modifiersJson,
        int parTimeMs,
        int goldTimeMs,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (levelSeedId == Guid.Empty)
        {
            throw new ArgumentException("Level seed ID is required.", nameof(levelSeedId));
        }

        var normalizedSeedValue = RequireText(seedValue, nameof(seedValue), 128, "Seed value is required.");
        var normalizedBiome = RequireText(biome, nameof(biome), 64, "Biome is required.");
        var normalizedTemplate = RequireText(template, nameof(template), 128, "Template is required.");
        var normalizedObjective = RequireText(objective, nameof(objective), 128, "Objective is required.");
        var normalizedModifiersJson = RequireText(modifiersJson, nameof(modifiersJson), 4096, "Modifiers JSON is required.");

        if (parTimeMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parTimeMs), "Par time must be greater than zero.");
        }

        if (goldTimeMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(goldTimeMs), "Gold time must be greater than zero.");
        }

        if (goldTimeMs > parTimeMs)
        {
            throw new ArgumentException("Gold time cannot exceed par time.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }

        return new LevelSeed(
            levelSeedId,
            normalizedSeedValue,
            normalizedBiome,
            normalizedTemplate,
            normalizedObjective,
            normalizedModifiersJson,
            parTimeMs,
            goldTimeMs,
            version,
            createdAt,
            updatedAt);
    }

    public void UpdateDefinition(
        string seedValue,
        string biome,
        string template,
        string objective,
        string modifiersJson,
        int parTimeMs,
        int goldTimeMs,
        DateTimeOffset updatedAt)
    {
        var updated = Rehydrate(LevelSeedId, seedValue, biome, template, objective, modifiersJson, parTimeMs, goldTimeMs, Version, CreatedAt, updatedAt);

        SeedValue = updated.SeedValue;
        Biome = updated.Biome;
        Template = updated.Template;
        Objective = updated.Objective;
        ModifiersJson = updated.ModifiersJson;
        ParTimeMs = updated.ParTimeMs;
        GoldTimeMs = updated.GoldTimeMs;
        UpdatedAt = updatedAt;
        Version++;
    }

    private static string RequireText(string value, string paramName, int maxLength, string requiredMessage)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(requiredMessage, paramName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }
}
