using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresMatchResultRepository : IMatchResultRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMatchResultRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<MatchResult> SaveAsync(MatchResult matchResult, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO match_results (
                match_result_id,
                account_id,
                season_id,
                level_seed_id,
                score,
                clear_time_ms,
                combo_max,
                penalties,
                validation_status,
                created_at,
                updated_at,
                version)
            VALUES (
                @matchResultId,
                @accountId,
                @seasonId,
                @levelSeedId,
                @score,
                @clearTimeMs,
                @comboMax,
                @penalties,
                @validationStatus,
                @createdAt,
                @updatedAt,
                @version)
            ON CONFLICT (match_result_id) DO UPDATE
            SET validation_status = EXCLUDED.validation_status,
                updated_at = EXCLUDED.updated_at,
                version = EXCLUDED.version
            RETURNING match_result_id, account_id, season_id, level_seed_id, score, clear_time_ms, combo_max, penalties, validation_status, created_at, updated_at, version;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("matchResultId", matchResult.MatchResultId);
        command.Parameters.AddWithValue("accountId", matchResult.AccountId);
        command.Parameters.AddWithValue("seasonId", matchResult.SeasonId);
        command.Parameters.AddWithValue("levelSeedId", matchResult.LevelSeedId);
        command.Parameters.AddWithValue("score", matchResult.Score);
        command.Parameters.AddWithValue("clearTimeMs", matchResult.ClearTimeMs);
        command.Parameters.AddWithValue("comboMax", matchResult.ComboMax);
        command.Parameters.AddWithValue("penalties", matchResult.Penalties);
        command.Parameters.AddWithValue("validationStatus", matchResult.ValidationStatus.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("createdAt", matchResult.CreatedAt);
        command.Parameters.AddWithValue("updatedAt", matchResult.UpdatedAt);
        command.Parameters.AddWithValue("version", matchResult.Version);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapMatchResult(reader);
    }

    public async Task<MatchResult?> GetByIdAsync(Guid matchResultId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT match_result_id, account_id, season_id, level_seed_id, score, clear_time_ms, combo_max, penalties, validation_status, created_at, updated_at, version
            FROM match_results
            WHERE match_result_id = @matchResultId
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("matchResultId", matchResultId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapMatchResult(reader);
    }

    private static MatchResult MapMatchResult(NpgsqlDataReader reader)
    {
        var matchResultId = reader.GetGuid(0);
        var accountId = reader.GetGuid(1);
        var seasonId = reader.GetGuid(2);
        var levelSeedId = reader.GetGuid(3);
        var score = reader.GetInt32(4);
        var clearTimeMs = reader.GetInt32(5);
        var comboMax = reader.GetInt32(6);
        var penalties = reader.GetInt32(7);
        var validationStatus = ParseValidationStatus(reader.GetString(8));
        var createdAt = reader.GetFieldValue<DateTimeOffset>(9);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(10);
        var version = reader.GetInt64(11);

        return MatchResult.Rehydrate(matchResultId, accountId, seasonId, levelSeedId, score, clearTimeMs, comboMax, penalties, validationStatus, createdAt, updatedAt, version);
    }

    private static MatchValidationStatus ParseValidationStatus(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pending" => MatchValidationStatus.Pending,
            "accepted" => MatchValidationStatus.Accepted,
            "rejected" => MatchValidationStatus.Rejected,
            _ => throw new InvalidOperationException($"Unsupported match validation status '{value}'.")
        };
    }
}
