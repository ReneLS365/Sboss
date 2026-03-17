namespace Sboss.Api.Tests;

public sealed class SchemaSanityTests
{
    [Fact]
    public void SchemaContainsRequiredTables()
    {
        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../db/schema.sql"));
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
}
