using System.Data;
using Npgsql;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresLoadoutRepository : ILoadoutRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresLoadoutRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<LoadoutSnapshot?> GetAsync(Guid accountId, Guid levelSeedId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await LoadAsync(connection, null, accountId, levelSeedId, cancellationToken);
    }

    public async Task<LoadoutSnapshot> UpsertAsync(LoadoutSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        const string upsertSessionSql = """
            INSERT INTO loadout_sessions (loadout_session_id, account_id, level_seed_id, max_capacity, used_capacity, is_complete, created_at, updated_at, version)
            VALUES (gen_random_uuid(), @accountId, @levelSeedId, @maxCapacity, @usedCapacity, @isComplete, NOW(), NOW(), 1)
            ON CONFLICT (account_id, level_seed_id)
            DO UPDATE SET max_capacity = EXCLUDED.max_capacity,
                          used_capacity = EXCLUDED.used_capacity,
                          is_complete = EXCLUDED.is_complete,
                          updated_at = NOW(),
                          version = loadout_sessions.version + 1
            RETURNING loadout_session_id;
            """;

        Guid loadoutSessionId;
        await using (var command = new NpgsqlCommand(upsertSessionSql, connection, transaction))
        {
            command.Parameters.AddWithValue("accountId", snapshot.AccountId);
            command.Parameters.AddWithValue("levelSeedId", snapshot.LevelSeedId);
            command.Parameters.AddWithValue("maxCapacity", snapshot.MaxCapacity);
            command.Parameters.AddWithValue("usedCapacity", snapshot.UsedCapacity);
            command.Parameters.AddWithValue("isComplete", snapshot.IsComplete);
            loadoutSessionId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }

        const string deleteItemsSql = """
            DELETE FROM loadout_session_items
            WHERE loadout_session_id = @loadoutSessionId;
            """;

        await using (var deleteCommand = new NpgsqlCommand(deleteItemsSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("loadoutSessionId", loadoutSessionId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertItemSql = """
            INSERT INTO loadout_session_items (loadout_session_item_id, loadout_session_id, item_code, quantity, created_at, updated_at, version)
            VALUES (gen_random_uuid(), @loadoutSessionId, @itemCode, @quantity, NOW(), NOW(), 1);
            """;

        foreach (var entry in snapshot.QuantitiesByItemCode.Where(pair => pair.Value > 0))
        {
            await using var insertCommand = new NpgsqlCommand(insertItemSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("loadoutSessionId", loadoutSessionId);
            insertCommand.Parameters.AddWithValue("itemCode", entry.Key);
            insertCommand.Parameters.AddWithValue("quantity", entry.Value);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await LoadAsync(connection, null, snapshot.AccountId, snapshot.LevelSeedId, cancellationToken)
            ?? snapshot;
    }

    private static async Task<LoadoutSnapshot?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid accountId,
        Guid levelSeedId,
        CancellationToken cancellationToken)
    {
        const string sessionSql = """
            SELECT max_capacity, used_capacity, is_complete
            FROM loadout_sessions
            WHERE account_id = @accountId
              AND level_seed_id = @levelSeedId
            LIMIT 1;
            """;

        int maxCapacity;
        int usedCapacity;
        bool isComplete;
        await using (var sessionCommand = new NpgsqlCommand(sessionSql, connection, transaction))
        {
            sessionCommand.Parameters.AddWithValue("accountId", accountId);
            sessionCommand.Parameters.AddWithValue("levelSeedId", levelSeedId);

            await using var reader = await sessionCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            maxCapacity = reader.GetInt32(0);
            usedCapacity = reader.GetInt32(1);
            isComplete = reader.GetBoolean(2);
        }

        const string itemsSql = """
            SELECT item_code, quantity
            FROM loadout_session_items lsi
            JOIN loadout_sessions ls ON ls.loadout_session_id = lsi.loadout_session_id
            WHERE ls.account_id = @accountId
              AND ls.level_seed_id = @levelSeedId;
            """;

        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var itemsCommand = new NpgsqlCommand(itemsSql, connection, transaction);
        itemsCommand.Parameters.AddWithValue("accountId", accountId);
        itemsCommand.Parameters.AddWithValue("levelSeedId", levelSeedId);
        await using var itemsReader = await itemsCommand.ExecuteReaderAsync(cancellationToken);
        while (await itemsReader.ReadAsync(cancellationToken))
        {
            quantities[itemsReader.GetString(0)] = itemsReader.GetInt32(1);
        }

        return new LoadoutSnapshot(accountId, levelSeedId, maxCapacity, usedCapacity, isComplete, quantities);
    }
}
