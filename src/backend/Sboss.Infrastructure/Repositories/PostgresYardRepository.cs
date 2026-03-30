using System.Data;
using Npgsql;
using Sboss.Infrastructure.Services;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresYardRepository : IYardRepository
{
    private const int DefaultYardCapacity = 1500;
    private const long IntegrityPerOwnedUnitBps = 10_000;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresYardRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<YardStateSnapshot?> GetSnapshotAsync(Guid accountId, IReadOnlyCollection<AuthoritativeComponentDefinition> supportedComponents, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureYardStateAsync(connection, null, accountId, cancellationToken);
        return await LoadSnapshotAsync(connection, null, accountId, supportedComponents, cancellationToken);
    }

    public async Task<PurchaseResult> PurchaseAsync(
        Guid accountId,
        AuthoritativeComponentDefinition component,
        int quantity,
        IReadOnlyCollection<AuthoritativeComponentDefinition> supportedComponents,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await EnsureYardStateAsync(connection, transaction, accountId, cancellationToken);
        await LockYardStateAsync(connection, transaction, accountId, cancellationToken);

        var snapshot = await LoadSnapshotAsync(connection, transaction, accountId, supportedComponents, cancellationToken);
        if (snapshot is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PurchaseResult(false, "missing_account", "Account does not exist.", null, 0);
        }

        var requiredCapacity = checked(component.UnitCapacity * quantity);
        if (requiredCapacity > snapshot.RemainingCapacity)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PurchaseResult(false, "capacity_overflow", "Purchase exceeds remaining yard capacity.", snapshot, GetOwnedQuantity(snapshot, component.ItemCode));
        }

        var totalCost = checked(component.PurchaseCost * quantity);
        if (snapshot.CoinBalance < totalCost)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PurchaseResult(false, "insufficient_funds", "Insufficient funds for purchase.", snapshot, GetOwnedQuantity(snapshot, component.ItemCode));
        }

        const string debitSql = """
            UPDATE account_balances
            SET balance = balance - @amount,
                updated_at = NOW(),
                version = version + 1
            WHERE account_id = @accountId
              AND currency_code = 'COIN'
              AND balance >= @amount;
            """;

        await using (var debitCommand = new NpgsqlCommand(debitSql, connection, transaction))
        {
            debitCommand.Parameters.AddWithValue("accountId", accountId);
            debitCommand.Parameters.AddWithValue("amount", totalCost);
            var rows = await debitCommand.ExecuteNonQueryAsync(cancellationToken);
            if (rows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PurchaseResult(false, "insufficient_funds", "Insufficient funds for purchase.", snapshot, GetOwnedQuantity(snapshot, component.ItemCode));
            }
        }

        const string upsertInventorySql = """
            INSERT INTO inventory_items (inventory_item_id, account_id, item_code, quantity, total_integrity_bps, created_at, updated_at, version)
            VALUES (gen_random_uuid(), @accountId, @itemCode, @quantity, @totalIntegrityBps, NOW(), NOW(), 1)
            ON CONFLICT (account_id, item_code)
            DO UPDATE SET quantity = inventory_items.quantity + EXCLUDED.quantity,
                          total_integrity_bps = inventory_items.total_integrity_bps + EXCLUDED.total_integrity_bps,
                          updated_at = NOW(),
                          version = inventory_items.version + 1;
            """;

        await using (var inventoryCommand = new NpgsqlCommand(upsertInventorySql, connection, transaction))
        {
            inventoryCommand.Parameters.AddWithValue("accountId", accountId);
            inventoryCommand.Parameters.AddWithValue("itemCode", component.ItemCode);
            inventoryCommand.Parameters.AddWithValue("quantity", quantity);
            inventoryCommand.Parameters.AddWithValue("totalIntegrityBps", checked((long)quantity * IntegrityPerOwnedUnitBps));
            await inventoryCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var updatedSnapshot = await LoadSnapshotAsync(connection, transaction, accountId, supportedComponents, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PurchaseResult(true, null, null, updatedSnapshot, GetOwnedQuantity(updatedSnapshot!, component.ItemCode));
    }

    public async Task ApplyWearAsync(Guid accountId, IReadOnlyDictionary<string, long> wearByItemCode, CancellationToken cancellationToken)
    {
        if (wearByItemCode.Count == 0)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        const string applyWearSql = """
            UPDATE inventory_items
            SET total_integrity_bps = GREATEST(0, total_integrity_bps - @wearBps),
                updated_at = NOW(),
                version = version + 1
            WHERE account_id = @accountId
              AND item_code = @itemCode
              AND quantity > 0;
            """;

        foreach (var entry in wearByItemCode)
        {
            if (entry.Value <= 0)
            {
                continue;
            }

            await using var command = new NpgsqlCommand(applyWearSql, connection, transaction);
            command.Parameters.AddWithValue("accountId", accountId);
            command.Parameters.AddWithValue("itemCode", entry.Key);
            command.Parameters.AddWithValue("wearBps", entry.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static int GetOwnedQuantity(YardStateSnapshot snapshot, string itemCode)
    {
        return snapshot.InventoryByItemCode.TryGetValue(itemCode, out var item) ? item.OwnedQuantity : 0;
    }

    private static async Task EnsureYardStateAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid accountId, CancellationToken cancellationToken)
    {
        const string ensureSql = """
            INSERT INTO yard_states (account_id, max_capacity, created_at, updated_at, version)
            SELECT account_id, @defaultCapacity, NOW(), NOW(), 1
            FROM accounts
            WHERE account_id = @accountId
            ON CONFLICT (account_id) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(ensureSql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("defaultCapacity", DefaultYardCapacity);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task LockYardStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        const string lockSql = """
            SELECT account_id
            FROM yard_states
            WHERE account_id = @accountId
            FOR UPDATE;
            """;

        await using var command = new NpgsqlCommand(lockSql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<YardStateSnapshot?> LoadSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid accountId,
        IReadOnlyCollection<AuthoritativeComponentDefinition> supportedComponents,
        CancellationToken cancellationToken)
    {
        const string snapshotSql = """
            SELECT ys.max_capacity,
                   COALESCE(ab.balance, 0) AS coin_balance
            FROM yard_states ys
            JOIN accounts a ON a.account_id = ys.account_id
            LEFT JOIN account_balances ab ON ab.account_id = ys.account_id AND ab.currency_code = 'COIN'
            WHERE ys.account_id = @accountId
            LIMIT 1;
            """;

        await using var snapshotCommand = new NpgsqlCommand(snapshotSql, connection, transaction);
        snapshotCommand.Parameters.AddWithValue("accountId", accountId);

        int maxCapacity;
        long coinBalance;
        await using (var snapshotReader = await snapshotCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await snapshotReader.ReadAsync(cancellationToken))
            {
                return null;
            }

            maxCapacity = snapshotReader.GetInt32(0);
            coinBalance = snapshotReader.GetInt64(1);
        }

        const string inventorySql = """
            SELECT item_code, quantity, total_integrity_bps
            FROM inventory_items
            WHERE account_id = @accountId;
            """;

        var inventory = new Dictionary<string, YardInventoryState>(StringComparer.OrdinalIgnoreCase);
        await using var inventoryCommand = new NpgsqlCommand(inventorySql, connection, transaction);
        inventoryCommand.Parameters.AddWithValue("accountId", accountId);
        await using var inventoryReader = await inventoryCommand.ExecuteReaderAsync(cancellationToken);
        while (await inventoryReader.ReadAsync(cancellationToken))
        {
            var quantity = inventoryReader.GetInt32(1);
            var totalIntegrityBps = inventoryReader.GetInt64(2);
            var usableQuantity = Math.Clamp((int)(totalIntegrityBps / IntegrityPerOwnedUnitBps), 0, quantity);
            var damagedQuantity = quantity - usableQuantity;
            inventory[inventoryReader.GetString(0)] = new YardInventoryState(quantity, usableQuantity, damagedQuantity, totalIntegrityBps);
        }

        var capacityByItemCode = supportedComponents.ToDictionary(component => component.ItemCode, component => component.UnitCapacity, StringComparer.OrdinalIgnoreCase);
        var usedCapacity = inventory.Sum(entry => capacityByItemCode.TryGetValue(entry.Key, out var unitCapacity) ? unitCapacity * entry.Value.UsableQuantity : 0);
        var remainingCapacity = Math.Max(0, maxCapacity - usedCapacity);
        return new YardStateSnapshot(accountId, maxCapacity, usedCapacity, remainingCapacity, coinBalance, inventory);
    }
}
