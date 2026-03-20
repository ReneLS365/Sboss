using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Sboss.Api.Tests;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = ResolveConnectionString();
        await ResetDatabaseAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task ResetDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required for repository integration tests.");
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };

        await using (var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await adminConnection.OpenAsync();
            await using (var terminateCommand = adminConnection.CreateCommand())
            {
                terminateCommand.CommandText = """
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = @databaseName
                      AND pid <> pg_backend_pid();
                    """;
                terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
                await terminateCommand.ExecuteNonQueryAsync();
            }

            await using (var dropCommand = adminConnection.CreateCommand())
            {
                dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
                await dropCommand.ExecuteNonQueryAsync();
            }

            await using var createCommand = adminConnection.CreateCommand();
            createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            await createCommand.ExecuteNonQueryAsync();
        }

        await using var targetConnection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString);
        await targetConnection.OpenAsync();

        var migrationSql = await File.ReadAllTextAsync(ResolveRepoPath("src/backend/db/migrations/0001_phase_1b_baseline.sql"));
        var seedSql = await File.ReadAllTextAsync(ResolveRepoPath("src/backend/db/seed.sql"));

        await using (var migrationCommand = targetConnection.CreateCommand())
        {
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
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(ResolveRepoPath("src/backend/Sboss.Api/appsettings.json"), optional: false)
            .AddEnvironmentVariables()
            .Build();

        return Environment.GetEnvironmentVariable("SBOSS_TEST_DATABASE")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Unable to resolve a PostgreSQL test connection string.");
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
