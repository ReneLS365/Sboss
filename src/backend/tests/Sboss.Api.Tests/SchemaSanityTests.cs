namespace Sboss.Api.Tests;

public sealed class SchemaSanityTests
{
    private const string ActiveSeasonId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
    private const string KnownLevelSeedId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
    private const string BaselineMigrationFileName = "0001_phase_1b_baseline.sql";
    private const string EconomyMigrationFileName = "0002_phase_1d_economy_tables.sql";

    [Fact]
    public void BaselineMigrationContainsPhase1BTables()
    {
        var migrationPath = ResolveMigrationPath(BaselineMigrationFileName);
        var migration = File.ReadAllText(migrationPath);

        var requiredTables = new[]
        {
            "schema_migrations",
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
            Assert.Contains($"CREATE TABLE IF NOT EXISTS {table}", migration, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EconomyMigrationContainsRequiredTables()
    {
        var migrationPath = ResolveMigrationPath(EconomyMigrationFileName);
        var migration = File.ReadAllText(migrationPath);

        Assert.Contains("CREATE TABLE IF NOT EXISTS account_balances", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS economy_transactions", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resulting_balance_version", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchemaSnapshotMatchesBaselineMigration()
    {
        var schema = NormalizeWhitespace(File.ReadAllText(ResolveSchemaPath()));
        var requiredStatements = new[] { BaselineMigrationFileName, EconomyMigrationFileName }
            .Select(ResolveMigrationPath)
            .SelectMany(path => File.ReadAllText(path)
                .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(statement => NormalizeWhitespace(statement)))
            .Where(statement => statement.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var statement in requiredStatements)
        {
            Assert.Contains(statement, schema, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SeedSql_ContainsDeterministicExpectedRows()
    {
        var seedPath = ResolveSeedPath();
        var seed = NormalizeWhitespace(File.ReadAllText(seedPath));

        Assert.Contains(
            NormalizeWhitespace("INSERT INTO seasons (season_id, name, starts_at, ends_at, is_active, created_at, updated_at, version) VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'Phase0-Season', '2026-01-01T00:00:00Z', '2026-12-31T23:59:59Z', TRUE, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)"),
            seed,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            NormalizeWhitespace("INSERT INTO level_seeds (level_seed_id, seed_value, biome, template, objective, modifiers_json, par_time_ms, gold_time_ms, version, created_at, updated_at) VALUES ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'SBOSS-SEED-001', 'urban', 'template_alpha', 'reach_target', '{\"modifiers\":[\"none\"]}', 120000, 90000, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z')"),
            seed,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            NormalizeWhitespace("INSERT INTO account_balances (account_id, currency_code, balance, created_at, updated_at, version) VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'COIN', 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)"),
            seed,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            NormalizeWhitespace("INSERT INTO economy_transactions (economy_transaction_id, account_id, currency_code, idempotency_key, amount_delta, resulting_balance, resulting_balance_version, reason, created_at, version) VALUES ('98989898-9898-9898-9898-989898989898', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'COIN', 'seed-opening-balance', 100, 100, 1, 'seed_opening_balance', '2026-01-01T00:00:00Z', 1)"),
            seed,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NOW()", seed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(KnownLevelSeedId, seed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ActiveSeasonId, seed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatabaseScriptsReferenceMigrationBaselineAndSeed()
    {
        var applyMigrations = File.ReadAllText(ResolveScriptPath("apply-migrations.sh"));
        var applySeed = File.ReadAllText(ResolveScriptPath("apply-seed.sh"));
        var validateBootstrap = File.ReadAllText(ResolveScriptPath("validate-bootstrap.sh"));
        var dockerInit = File.ReadAllText(ResolveScriptPath("docker-init.sh"));

        Assert.Contains("schema_migrations", applyMigrations, StringComparison.Ordinal);
        Assert.Contains("/migrations", applyMigrations, StringComparison.Ordinal);
        Assert.Contains("seed.sql", applySeed, StringComparison.Ordinal);
        Assert.Contains("apply-migrations.sh", validateBootstrap, StringComparison.Ordinal);
        Assert.Contains("apply-seed.sh", validateBootstrap, StringComparison.Ordinal);
        Assert.Contains("postgresql://${POSTGRES_USER}:${POSTGRES_PASSWORD}@/${POSTGRES_DB}", dockerInit, StringComparison.Ordinal);
        Assert.DoesNotContain("@localhost", dockerInit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("apply-migrations.sh", dockerInit, StringComparison.Ordinal);
        Assert.Contains("apply-seed.sh", dockerInit, StringComparison.Ordinal);
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

    private static string ResolveMigrationPath(string migrationFileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "db", "migrations", migrationFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Unable to locate db/migrations/{migrationFileName} by traversing parent directories.");
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

    private static string ResolveScriptPath(string scriptFileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "db", "scripts", scriptFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Unable to locate db/scripts/{scriptFileName} by traversing parent directories.");
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
