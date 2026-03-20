using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresSeasonRepository : ISeasonRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSeasonRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Season> GetCurrentSeasonAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT season_id, name, starts_at, ends_at, is_active, created_at, updated_at, version
            FROM seasons
            WHERE is_active = TRUE
            ORDER BY starts_at DESC
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("No active season exists in persistence.");
        }

        return MapSeason(reader);
    }

    public async Task<Season?> GetByIdAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT season_id, name, starts_at, ends_at, is_active, created_at, updated_at, version
            FROM seasons
            WHERE season_id = @seasonId
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("seasonId", seasonId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapSeason(reader);
    }

    private static Season MapSeason(NpgsqlDataReader reader)
    {
        var seasonId = reader.GetGuid(0);
        var name = reader.GetString(1);
        var startsAt = reader.GetFieldValue<DateTimeOffset>(2);
        var endsAt = reader.GetFieldValue<DateTimeOffset>(3);
        var isActive = reader.GetBoolean(4);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(5);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(6);
        var version = reader.GetInt64(7);

        return Season.Rehydrate(seasonId, name, startsAt, endsAt, isActive, createdAt, updatedAt, version);
    }
}
