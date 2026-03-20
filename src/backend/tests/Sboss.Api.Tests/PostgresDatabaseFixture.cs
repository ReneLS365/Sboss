using Npgsql;

namespace Sboss.Api.Tests;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = ResolveConnectionString();
        await ResetAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public Task ResetAsync()
    {
        return ResetDatabaseAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync();

        var baselineMigrationSql = await File.ReadAllTextAsync(ResolveRepoPath("src/backend/db/migrations/0001_phase_1b_baseline.sql"));
        var economyMigrationSql = await File.ReadAllTextAsync(ResolveRepoPath("src/backend/db/migrations/0002_phase_1d_economy_tables.sql"));
        var seedSql = await File.ReadAllTextAsync(ResolveRepoPath("src/backend/db/seed.sql"));

        await using (var bootstrapBaselineCommand = connection.CreateCommand())
        {
            bootstrapBaselineCommand.CommandText = baselineMigrationSql;
            await bootstrapBaselineCommand.ExecuteNonQueryAsync();
        }

        await using (var bootstrapEconomyCommand = connection.CreateCommand())
        {
            bootstrapEconomyCommand.CommandText = economyMigrationSql;
            await bootstrapEconomyCommand.ExecuteNonQueryAsync();
        }

        const string truncateSql = """
            TRUNCATE accounts, player_profiles, inventory_items, cosmetic_unlocks,
                     seasons, level_seeds, match_results,
                     account_balances, economy_transactions
            RESTART IDENTITY CASCADE;
            """;

        await using (var truncateCommand = connection.CreateCommand())
        {
            truncateCommand.CommandText = truncateSql;
            await truncateCommand.ExecuteNonQueryAsync();
        }

        await using (var baselineMigrationCommand = connection.CreateCommand())
        {
            baselineMigrationCommand.CommandText = baselineMigrationSql;
            await baselineMigrationCommand.ExecuteNonQueryAsync();
        }

        await using (var economyMigrationCommand = connection.CreateCommand())
        {
            economyMigrationCommand.CommandText = economyMigrationSql;
            await economyMigrationCommand.ExecuteNonQueryAsync();
        }

        await using (var seedCommand = connection.CreateCommand())
        {
            seedCommand.CommandText = seedSql;
            await seedCommand.ExecuteNonQueryAsync();
        }
    }

    private static string ResolveConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("SBOSS_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "SBOSS_TEST_DATABASE must be set to an isolated PostgreSQL database before running repository integration tests.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.Equals(builder.Database, "sboss", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SBOSS_TEST_DATABASE must not target the default 'sboss' development database.");
        }

        return connectionString;
    }

    private static string ResolveRepoPath(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Unable to locate '{relativePath}' from test base directory.");
    }
}
