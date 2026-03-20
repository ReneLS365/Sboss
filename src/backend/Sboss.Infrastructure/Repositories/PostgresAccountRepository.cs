using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresAccountRepository : IAccountRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT account_id, external_ref, created_at, updated_at, version
            FROM accounts
            WHERE account_id = @accountId
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapAccount(reader);
    }

    private static Account MapAccount(NpgsqlDataReader reader)
    {
        var accountId = reader.GetGuid(0);
        var externalRef = reader.GetString(1);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(2);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(3);
        var version = reader.GetInt64(4);

        return Account.Rehydrate(accountId, externalRef, createdAt, updatedAt, version);
    }
}
