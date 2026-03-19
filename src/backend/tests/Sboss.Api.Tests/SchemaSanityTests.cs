namespace Sboss.Api.Tests;

public sealed class SchemaSanityTests
{
    private const string ActiveSeasonId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
    private const string KnownLevelSeedId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
    private const string SeasonsInsertPrefix = "INSERT INTO seasons (season_id, name, starts_at, ends_at, is_active)";
    private const string LevelSeedsInsertPrefix = "INSERT INTO level_seeds (level_seed_id, seed_value, biome, template, objective, modifiers_json, par_time_ms, gold_time_ms, version)";

    [Fact]
    public void SchemaContainsRequiredTables()
    {
        var schemaPath = ResolveSchemaPath();
        var schema = File.ReadAllText(schemaPath);

        var requiredTables = new[]
        {
            "accounts",
            "player_profiles",
            "inventory_items",
            "level_seeds",
            "seasons",
            "cosmetic_unlocks",
            "match_results"
        };

        foreach (var table in requiredTables)
        {
            Assert.Contains($"CREATE TABLE IF NOT EXISTS {table}", schema, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SeedSql_ContainsExpectedSeasonAndLevelSeedRows()
    {
        var seedPath = ResolveSeedPath();
        var seed = NormalizeWhitespace(File.ReadAllText(seedPath));

        Assert.Contains(
            NormalizeWhitespace($"{SeasonsInsertPrefix} VALUES ('{ActiveSeasonId}',"),
            seed,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            NormalizeWhitespace($"{LevelSeedsInsertPrefix} VALUES ('{KnownLevelSeedId}',"),
            seed,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSchemaPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "db", "schema.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Unable to locate db/schema.sql by traversing parent directories.");
    }

    private static string ResolveSeedPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "db", "seed.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Unable to locate db/seed.sql by traversing parent directories.");
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
