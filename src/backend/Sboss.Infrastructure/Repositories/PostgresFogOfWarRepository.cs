using Npgsql;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresFogOfWarRepository : IFogOfWarRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresFogOfWarRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<string>> GetRevealedKeysAsync(Guid accountId, Guid levelSeedId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT reveal_key
            FROM fog_of_war_states
            WHERE account_id = @accountId
              AND level_seed_id = @levelSeedId
            ORDER BY reveal_key;
            """;

        var keys = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("levelSeedId", levelSeedId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    public async Task<bool> RevealAsync(Guid accountId, Guid levelSeedId, string revealKey, DateTimeOffset revealedAt, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            INSERT INTO fog_of_war_states (fog_of_war_state_id, account_id, level_seed_id, reveal_key, revealed_at, created_at, updated_at, version)
            VALUES (gen_random_uuid(), @accountId, @levelSeedId, @revealKey, @revealedAt, NOW(), NOW(), 1)
            ON CONFLICT (account_id, level_seed_id, reveal_key)
            DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("levelSeedId", levelSeedId);
        command.Parameters.AddWithValue("revealKey", revealKey);
        command.Parameters.AddWithValue("revealedAt", revealedAt);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        return changed > 0;
    }
}
