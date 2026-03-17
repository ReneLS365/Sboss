namespace Sboss.Api.Tests;

public sealed class SchemaSanityTests
{
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
}
