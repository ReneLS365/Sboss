using Npgsql;
using Sboss.Domain.Entities;
using Sboss.Infrastructure;
using Sboss.Infrastructure.Repositories;
using System.Text.Json;

namespace Sboss.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class RepositoryIntegrationTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeasonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid LevelSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ContractJobId = Guid.Parse("f1e00000-0000-0000-0000-000000000001");
    private readonly PostgresDatabaseFixture _database;

    public RepositoryIntegrationTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task AccountRepository_ReadsSeededDomainEntity()
    {
        await using var dataSource = NpgsqlDataSourceRegistry.Create(_database.ConnectionString);
        var repository = new PostgresAccountRepository(dataSource);

        var account = await repository.GetByIdAsync(AccountId, CancellationToken.None);

        Assert.NotNull(account);
        Assert.Equal(AccountId, account!.AccountId);
        Assert.Equal("phase0-account", account.ExternalRef);
        Assert.Equal(1, account.Version);
    }

    [Fact]
    public async Task SeasonRepository_ReadsCurrentSeededSeason()
    {
        await using var dataSource = NpgsqlDataSourceRegistry.Create(_database.ConnectionString);
        var repository = new PostgresSeasonRepository(dataSource);

        var season = await repository.GetCurrentSeasonAsync(CancellationToken.None);

        Assert.Equal(SeasonId, season.SeasonId);
        Assert.Equal("Phase0-Season", season.Name);
        Assert.True(season.IsActive);
        Assert.Equal(1, season.Version);
    }

    [Fact]
    public async Task LevelSeedRepository_ReadsSeededLevelSeed()
    {
        await using var dataSource = NpgsqlDataSourceRegistry.Create(_database.ConnectionString);
        var repository = new PostgresLevelSeedRepository(dataSource);

        var seed = await repository.GetByIdAsync(LevelSeedId, CancellationToken.None);

        Assert.NotNull(seed);
        Assert.Equal(LevelSeedId, seed!.LevelSeedId);
        Assert.Equal("SBOSS-SEED-001", seed.SeedValue);
        Assert.Equal("{\"modifiers\":[\"none\"]}", NormalizeJson(seed.ModifiersJson));
        Assert.Equal(1, seed.Version);
    }

    [Fact]
    public async Task MatchResultRepository_RoundTripsDomainEntityAgainstMigrationBaseline()
    {
        await using var dataSource = NpgsqlDataSourceRegistry.Create(_database.ConnectionString);
        var repository = new PostgresMatchResultRepository(dataSource);
        var createdAt = DateTimeOffset.Parse("2026-03-19T00:00:00Z");
        var matchResult = MatchResult.Create(AccountId, SeasonId, LevelSeedId, 4321, 87000, 15, 1, createdAt);
        matchResult.ApplyValidation(MatchValidationStatus.Accepted, createdAt.AddSeconds(5));

        var saved = await repository.SaveAsync(matchResult, CancellationToken.None);
        var reloaded = await repository.GetByIdAsync(saved.MatchResultId, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(saved.MatchResultId, reloaded!.MatchResultId);
        Assert.Equal(AccountId, reloaded.AccountId);
        Assert.Equal(SeasonId, reloaded.SeasonId);
        Assert.Equal(LevelSeedId, reloaded.LevelSeedId);
        Assert.Equal(4321, reloaded.Score);
        Assert.Equal(87000, reloaded.ClearTimeMs);
        Assert.Equal(MatchValidationStatus.Accepted, reloaded.ValidationStatus);
        Assert.Equal(2, reloaded.Version);
    }

    [Fact]
    public async Task ContractJobRepository_ReadsPersistedAuthoritativeState()
    {
        await _database.ResetAsync();
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();

        const string insertSql = """
            INSERT INTO contract_jobs (contract_job_id, owning_account_id, current_state, created_at, updated_at, version)
            VALUES (@contractJobId, @accountId, 'Accepted', @createdAt, @updatedAt, 3);
            """;

        await using (var insertCommand = new NpgsqlCommand(insertSql, connection))
        {
            insertCommand.Parameters.AddWithValue("contractJobId", ContractJobId);
            insertCommand.Parameters.AddWithValue("accountId", AccountId);
            insertCommand.Parameters.AddWithValue("createdAt", DateTimeOffset.Parse("2026-03-22T00:00:00Z"));
            insertCommand.Parameters.AddWithValue("updatedAt", DateTimeOffset.Parse("2026-03-22T00:02:00Z"));
            await insertCommand.ExecuteNonQueryAsync();
        }

        await using var dataSource = NpgsqlDataSourceRegistry.Create(_database.ConnectionString);
        var repository = new PostgresContractJobRepository(dataSource);

        var job = await repository.GetByIdAsync(ContractJobId, CancellationToken.None);

        Assert.NotNull(job);
        Assert.Equal(ContractJobId, job!.ContractJobId);
        Assert.Equal(AccountId, job.OwningAccountId);
        Assert.Equal(ContractJobState.Accepted, job.CurrentState);
        Assert.Equal(3, job.Version);
    }

    private static string NormalizeJson(string json)
    {
        return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
    }
}
