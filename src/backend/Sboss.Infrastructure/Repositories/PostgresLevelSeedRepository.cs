using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresLevelSeedRepository : ILevelSeedRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresLevelSeedRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<LevelSeed?> GetByIdAsync(Guid seedId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT level_seed_id, seed_value, biome, template, objective, modifiers_json::text, par_time_ms, gold_time_ms, version, created_at, updated_at
            FROM level_seeds
            WHERE level_seed_id = @seedId
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("seedId", seedId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapLevelSeed(reader);
    }

    private static LevelSeed MapLevelSeed(NpgsqlDataReader reader)
    {
        var levelSeedId = reader.GetGuid(0);
        var seedValue = reader.GetString(1);
        var biome = reader.GetString(2);
        var template = reader.GetString(3);
        var objective = reader.GetString(4);
        var modifiersJson = reader.GetString(5);
        var parTimeMs = reader.GetInt32(6);
        var goldTimeMs = reader.GetInt32(7);
        var version = reader.GetInt32(8);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(9);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(10);

        return LevelSeed.Rehydrate(levelSeedId, seedValue, biome, template, objective, modifiersJson, parTimeMs, goldTimeMs, version, createdAt, updatedAt);
    }
}
