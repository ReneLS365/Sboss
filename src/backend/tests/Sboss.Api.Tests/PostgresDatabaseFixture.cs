using Npgsql;
using Sboss.Infrastructure;

namespace Sboss.Api.Tests;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private static readonly string[] MigrationFiles =
    {
        "src/backend/db/migrations/0001_phase_1b_baseline.sql",
        "src/backend/db/migrations/0002_phase_1d_economy_tables.sql",
        "src/backend/db/migrations/0003_phase_1e_contract_jobs.sql",
        "src/backend/db/migrations/0004_phase_1f_contract_job_applications.sql",
        "src/backend/db/migrations/0005_phase_3a_yard_capacity_inventory.sql"
    };

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
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required for repository integration tests.");
        }

        var nonPoolingConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString;

        await NpgsqlDataSourceRegistry.DisposeTrackedDataSourcesAsync();
        NpgsqlConnection.ClearAllPools();

        await using (var resetConnection = new NpgsqlConnection(nonPoolingConnectionString))
        {
            await resetConnection.OpenAsync();

            await using var resetSchemaCommand = resetConnection.CreateCommand();
            resetSchemaCommand.CommandText = """
                DROP SCHEMA IF EXISTS public CASCADE;
                CREATE SCHEMA public;
                GRANT ALL ON SCHEMA public TO CURRENT_USER;
                GRANT ALL ON SCHEMA public TO PUBLIC;
                """;
            await resetSchemaCommand.ExecuteNonQueryAsync();
        }

        await using var targetConnection = new NpgsqlConnection(nonPoolingConnectionString);
        await targetConnection.OpenAsync();

        var seedSql = await File.ReadAllTextAsync(ResolveRepoPath("src/backend/db/seed.sql"));
        foreach (var migrationFile in MigrationFiles)
        {
            var migrationSql = await File.ReadAllTextAsync(ResolveRepoPath(migrationFile));
            await using var migrationCommand = targetConnection.CreateCommand();
            migrationCommand.CommandText = migrationSql;
            await migrationCommand.ExecuteNonQueryAsync();
        }

        await using (var seedCommand = targetConnection.CreateCommand())
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
