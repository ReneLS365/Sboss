using System.Data;
using Npgsql;
using Sboss.Domain.Entities;
using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure.Services;

public sealed class ProgressionService : IProgressionService
{
    private const int BaseCompletionXp = 100;
    private static readonly IReadOnlyDictionary<string, int> TemplateDifficultyBps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["template_alpha"] = 10_000,
        ["template_beta"] = 12_000,
        ["template_offshore_rotation"] = 15_000
    };

    private static readonly IReadOnlyDictionary<string, int> TemplateUnlockLevel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["template_alpha"] = 1,
        ["template_beta"] = 2,
        ["template_offshore_rotation"] = 3
    };

    private static readonly (int Level, long MinXp)[] LevelThresholds =
    {
        (1, 0),
        (2, 200),
        (3, 500),
        (4, 900),
        (5, 1400)
    };

    private readonly NpgsqlDataSource _dataSource;
    private readonly IMatchResultRepository _matchResultRepository;
    private readonly ILevelSeedRepository _levelSeedRepository;
    private readonly IAccountRepository _accountRepository;

    public ProgressionService(
        NpgsqlDataSource dataSource,
        IMatchResultRepository matchResultRepository,
        ILevelSeedRepository levelSeedRepository,
        IAccountRepository accountRepository)
    {
        _dataSource = dataSource;
        _matchResultRepository = matchResultRepository;
        _levelSeedRepository = levelSeedRepository;
        _accountRepository = accountRepository;
    }

    public async Task<ProgressionState> GetStateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new InvalidOperationException("Account ID is required.");
        }

        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Account does not exist.");
        }

        return await GetOrCreateStateAsync(accountId, cancellationToken);
    }

    public async Task<ProgressionAwardResult> AwardFromMatchResultAsync(Guid accountId, Guid matchResultId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || matchResultId == Guid.Empty)
        {
            throw new InvalidOperationException("Account ID and match result ID are required.");
        }

        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException("Account does not exist.");
        }

        var matchResult = await _matchResultRepository.GetByIdAsync(matchResultId, cancellationToken);
        if (matchResult is null)
        {
            throw new InvalidOperationException("Match result does not exist.");
        }

        if (matchResult.AccountId != accountId)
        {
            throw new InvalidOperationException("Match result does not belong to the requested account.");
        }

        if (matchResult.ValidationStatus != MatchValidationStatus.Accepted)
        {
            throw new InvalidOperationException("XP can only be awarded for accepted match results.");
        }

        var levelSeed = await _levelSeedRepository.GetByIdAsync(matchResult.LevelSeedId, cancellationToken);
        if (levelSeed is null)
        {
            throw new InvalidOperationException("Level seed does not exist.");
        }

        var difficultyBps = TemplateDifficultyBps.TryGetValue(levelSeed.Template, out var configuredDifficulty)
            ? configuredDifficulty
            : 10_000;
        var difficultyXp = (BaseCompletionXp * difficultyBps) / 10_000;
        var performanceBonus = ComputePerformanceBonus(matchResult);
        var totalAwardXp = checked(difficultyXp + performanceBonus);
        var awardedAt = DateTimeOffset.UtcNow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var existingAward = await GetAwardRecordAsync(connection, transaction, accountId, matchResultId, cancellationToken);
        if (existingAward is not null)
        {
            var state = await GetOrCreateStateAsync(connection, transaction, accountId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProgressionAwardResult(
                accountId,
                matchResultId,
                existingAward.XpAwarded,
                state.TotalXp,
                state.Level,
                false,
                true);
        }

        var current = await GetOrCreateStateAsync(connection, transaction, accountId, cancellationToken);
        var updatedTotalXp = checked(current.TotalXp + totalAwardXp);
        var updatedLevel = EvaluateLevel(updatedTotalXp);
        var leveledUp = updatedLevel > current.Level;

        await UpsertProgressionStateAsync(connection, transaction, accountId, updatedTotalXp, updatedLevel, awardedAt, cancellationToken);
        await InsertAwardRecordAsync(
            connection,
            transaction,
            accountId,
            matchResultId,
            matchResult.LevelSeedId,
            difficultyXp,
            difficultyBps,
            performanceBonus,
            totalAwardXp,
            awardedAt,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new ProgressionAwardResult(accountId, matchResultId, totalAwardXp, updatedTotalXp, updatedLevel, leveledUp, false);
    }

    public bool IsTemplateUnlocked(string templateCode, int playerLevel)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
        {
            return false;
        }

        var requiredLevel = TemplateUnlockLevel.TryGetValue(templateCode, out var configuredLevel) ? configuredLevel : 1;
        return playerLevel >= requiredLevel;
    }

    public IReadOnlyList<string> GetUnlockedTemplates(int playerLevel)
    {
        return TemplateUnlockLevel
            .Where(pair => playerLevel >= pair.Value)
            .Select(pair => pair.Key)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public long? GetNextLevelXpRequired(long totalXp)
    {
        foreach (var (_, minXp) in LevelThresholds)
        {
            if (minXp > totalXp)
            {
                return minXp;
            }
        }

        return null;
    }

    private static int ComputePerformanceBonus(MatchResult result)
    {
        var comboBonus = Math.Min(result.ComboMax, 20) * 2;
        var penaltyReduction = result.Penalties * 10;
        return Math.Max(0, comboBonus - penaltyReduction);
    }

    private static int EvaluateLevel(long totalXp)
    {
        var level = 1;
        foreach (var threshold in LevelThresholds)
        {
            if (totalXp >= threshold.MinXp)
            {
                level = threshold.Level;
            }
        }

        return level;
    }

    private async Task<ProgressionState> GetOrCreateStateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var state = await GetOrCreateStateAsync(connection, transaction, accountId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return state;
    }

    private static async Task<ProgressionState> GetOrCreateStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var state = await GetStateRecordAsync(connection, transaction, accountId, cancellationToken);
        if (state is not null)
        {
            return state;
        }

        const string insertSql = """
            INSERT INTO account_progression (account_id, total_xp, level, created_at, updated_at, version)
            VALUES (@accountId, 0, 1, NOW(), NOW(), 1)
            ON CONFLICT (account_id) DO NOTHING;
            """;

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("accountId", accountId);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        return await GetStateRecordAsync(connection, transaction, accountId, cancellationToken)
            ?? new ProgressionState(accountId, 0, 1, 1);
    }

    private static async Task<ProgressionState?> GetStateRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT account_id, total_xp, level, version
            FROM account_progression
            WHERE account_id = @accountId
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(selectSql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProgressionState(
            reader.GetGuid(0),
            reader.GetInt64(1),
            reader.GetInt32(2),
            reader.GetInt64(3));
    }

    private static async Task UpsertProgressionStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        long totalXp,
        int level,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO account_progression (account_id, total_xp, level, created_at, updated_at, version)
            VALUES (@accountId, @totalXp, @level, @updatedAt, @updatedAt, 1)
            ON CONFLICT (account_id) DO UPDATE
            SET total_xp = EXCLUDED.total_xp,
                level = EXCLUDED.level,
                updated_at = EXCLUDED.updated_at,
                version = account_progression.version + 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("totalXp", totalXp);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("updatedAt", updatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAwardRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid matchResultId,
        Guid levelSeedId,
        int baseXp,
        int difficultyBps,
        int performanceBonus,
        int xpAwarded,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO progression_xp_awards (
                progression_xp_award_id,
                account_id,
                match_result_id,
                level_seed_id,
                base_xp,
                difficulty_bps,
                performance_bonus_xp,
                xp_awarded,
                created_at)
            VALUES (
                gen_random_uuid(),
                @accountId,
                @matchResultId,
                @levelSeedId,
                @baseXp,
                @difficultyBps,
                @performanceBonus,
                @xpAwarded,
                @createdAt);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("matchResultId", matchResultId);
        command.Parameters.AddWithValue("levelSeedId", levelSeedId);
        command.Parameters.AddWithValue("baseXp", baseXp);
        command.Parameters.AddWithValue("difficultyBps", difficultyBps);
        command.Parameters.AddWithValue("performanceBonus", performanceBonus);
        command.Parameters.AddWithValue("xpAwarded", xpAwarded);
        command.Parameters.AddWithValue("createdAt", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ProgressionAwardRecord?> GetAwardRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid accountId,
        Guid matchResultId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT xp_awarded
            FROM progression_xp_awards
            WHERE account_id = @accountId
              AND match_result_id = @matchResultId
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        command.Parameters.AddWithValue("matchResultId", matchResultId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProgressionAwardRecord(reader.GetInt32(0));
    }

    private sealed record ProgressionAwardRecord(int XpAwarded);
}
