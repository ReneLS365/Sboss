using System.Data;
using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed class CrewService : ICrewService
{
    private const int CrewShareRatioBps = 6000;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEconomyTransactionService _economyTransactionService;

    public CrewService(NpgsqlDataSource dataSource, IEconomyTransactionService economyTransactionService)
    {
        _dataSource = dataSource;
        _economyTransactionService = economyTransactionService;
    }

    public async Task<Crew> CreateCrewAsync(Guid ownerAccountId, string name, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var crew = Crew.Create(ownerAccountId, name, now);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        if (!await AccountExistsAsync(connection, transaction, crew.OwnerAccountId, cancellationToken))
        {
            throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Owner account does not exist.");
        }

        const string sql = """
            INSERT INTO crews (crew_id, owner_account_id, name, created_at, updated_at, version)
            VALUES (@crewId, @ownerAccountId, @name, @createdAt, @updatedAt, @version);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("crewId", crew.CrewId);
        command.Parameters.AddWithValue("ownerAccountId", crew.OwnerAccountId);
        command.Parameters.AddWithValue("name", crew.Name);
        command.Parameters.AddWithValue("createdAt", crew.CreatedAt);
        command.Parameters.AddWithValue("updatedAt", crew.UpdatedAt);
        command.Parameters.AddWithValue("version", crew.Version);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return crew;
    }

    public async Task<CrewMember> AssignMemberAsync(Guid crewId, Guid actorAccountId, Guid memberAccountId, CrewRole role, CancellationToken cancellationToken)
    {
        if (crewId == Guid.Empty || actorAccountId == Guid.Empty || memberAccountId == Guid.Empty)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Crew ID, actor account ID, and member account ID are required.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Crew role is invalid.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var crew = await GetCrewAsync(connection, transaction, crewId, lockForUpdate: true, cancellationToken)
            ?? throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Crew does not exist.");
        EnsureOwnerActor(crew, actorAccountId);

        if (!await AccountExistsAsync(connection, transaction, memberAccountId, cancellationToken))
        {
            throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Member account does not exist.");
        }

        var now = DateTimeOffset.UtcNow;
        const string sql = """
            INSERT INTO crew_members (crew_id, account_id, role, created_at, updated_at, version)
            VALUES (@crewId, @accountId, @role, @createdAt, @updatedAt, 1)
            ON CONFLICT (crew_id, account_id) DO UPDATE
            SET role = EXCLUDED.role,
                updated_at = EXCLUDED.updated_at,
                version = crew_members.version + 1
            RETURNING crew_id, account_id, role, created_at, updated_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("crewId", crewId);
        command.Parameters.AddWithValue("accountId", memberAccountId);
        command.Parameters.AddWithValue("role", role.ToString());
        command.Parameters.AddWithValue("createdAt", now);
        command.Parameters.AddWithValue("updatedAt", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var saved = MapCrewMember(reader);
        await reader.CloseAsync();

        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    public async Task RemoveMemberAsync(Guid crewId, Guid actorAccountId, Guid memberAccountId, CancellationToken cancellationToken)
    {
        if (crewId == Guid.Empty || actorAccountId == Guid.Empty || memberAccountId == Guid.Empty)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Crew ID, actor account ID, and member account ID are required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var crew = await GetCrewAsync(connection, transaction, crewId, lockForUpdate: true, cancellationToken)
            ?? throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Crew does not exist.");
        if (actorAccountId != crew.OwnerAccountId && actorAccountId != memberAccountId)
        {
            throw new CrewServiceException(CrewServiceFailureReason.Forbidden, "Only the crew owner or the member can remove this membership.");
        }

        const string sql = """
            DELETE FROM crew_members
            WHERE crew_id = @crewId AND account_id = @memberAccountId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("crewId", crewId);
        command.Parameters.AddWithValue("memberAccountId", memberAccountId);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Crew member does not exist.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CrewSplitResult> PreviewSplitAsync(Guid crewId, long grossAmount, CancellationToken cancellationToken)
    {
        if (crewId == Guid.Empty)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Crew ID is required.");
        }

        if (grossAmount <= 0)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Gross amount must be greater than zero.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var crew = await GetCrewAsync(connection, transaction, crewId, lockForUpdate: false, cancellationToken)
            ?? throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Crew does not exist.");
        var members = await GetCrewMembersAsync(connection, transaction, crew.CrewId, cancellationToken);
        var result = CalculateSplit(crew.CrewId, grossAmount, members);

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<CrewSplitResult> SettlePayoutAsync(
        Guid crewId,
        Guid actorAccountId,
        long grossAmount,
        string currencyCode,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken)
    {
        if (actorAccountId == Guid.Empty)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Actor account ID is required.");
        }

        var normalizedCurrencyCode = NormalizeTrimmedValue(currencyCode, nameof(currencyCode), "Currency code");
        var normalizedIdempotencyKey = NormalizeTrimmedValue(idempotencyKey, nameof(idempotencyKey), "Idempotency key");
        var normalizedReason = NormalizeTrimmedValue(reason, nameof(reason), "Reason");
        var split = await PreviewSplitAsync(crewId, grossAmount, cancellationToken);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var crew = await GetCrewAsync(connection, transaction, crewId, lockForUpdate: false, cancellationToken)
            ?? throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Crew does not exist.");
        EnsureOwnerActor(crew, actorAccountId);
        await transaction.CommitAsync(cancellationToken);

        var payoutMutations = new List<EconomyMutationRequest>(split.Members.Count + 1);
        foreach (var member in split.Members)
        {
            payoutMutations.Add(
                new EconomyMutationRequest(
                    member.AccountId,
                    normalizedCurrencyCode,
                    member.Amount,
                    $"{normalizedIdempotencyKey}:{member.AccountId:N}",
                    $"{normalizedReason}:crew:{crewId:N}:member:{member.AccountId:N}"));
        }

        payoutMutations.Add(
            new EconomyMutationRequest(
                crew.OwnerAccountId,
                normalizedCurrencyCode,
                split.CompanyShareAmount,
                $"{normalizedIdempotencyKey}:{crew.OwnerAccountId:N}:company",
                $"{normalizedReason}:crew:{crewId:N}:company"));

        await _economyTransactionService.ApplyBatchAsync(payoutMutations, cancellationToken);

        return split;
    }

    internal static CrewSplitResult CalculateSplit(Guid crewId, long grossAmount, IReadOnlyList<CrewMember> members)
    {
        if (grossAmount <= 0)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Gross amount must be greater than zero.");
        }

        if (members.Count == 0)
        {
            throw new CrewServiceException(CrewServiceFailureReason.Conflict, "Crew must have at least one member before calculating split.");
        }

        var companyAmount = grossAmount * (10_000 - CrewShareRatioBps) / 10_000;
        var crewAmount = grossAmount - companyAmount;

        var orderedMembers = members.OrderBy(member => member.AccountId).ToArray();
        var totalWeight = orderedMembers.Sum(member => GetRoleWeight(member.Role));
        if (totalWeight <= 0)
        {
            throw new CrewServiceException(CrewServiceFailureReason.Conflict, "Crew role weights must produce a positive total weight.");
        }

        var memberBreakdown = new List<CrewSplitMemberResult>(orderedMembers.Length);
        long allocated = 0;
        foreach (var member in orderedMembers)
        {
            var ratioWeight = GetRoleWeight(member.Role);
            var amount = crewAmount * ratioWeight / totalWeight;
            allocated += amount;
            memberBreakdown.Add(new CrewSplitMemberResult(member.AccountId, member.Role, ratioWeight, amount));
        }

        var remainder = crewAmount - allocated;
        for (var index = 0; index < memberBreakdown.Count && remainder > 0; index++)
        {
            memberBreakdown[index] = memberBreakdown[index] with { Amount = memberBreakdown[index].Amount + 1 };
            remainder--;
        }

        return new CrewSplitResult(
            crewId,
            grossAmount,
            CrewShareRatioBps,
            crewAmount,
            companyAmount,
            memberBreakdown);
    }

    private static int GetRoleWeight(CrewRole role)
    {
        return role switch
        {
            CrewRole.Svend => 2,
            CrewRole.Laerling => 1,
            _ => throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, $"Unsupported crew role '{role}'.")
        };
    }

    private static string NormalizeTrimmedValue(string value, string parameterName, string label)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, $"{label} is required.");
        }

        if (normalized.Length > 128)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, $"{label} must be 128 characters or fewer.");
        }

        return normalized;
    }

    private static async Task<bool> AccountExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM accounts
            WHERE account_id = @accountId
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("accountId", accountId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<Crew?> GetCrewAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid crewId, bool lockForUpdate, CancellationToken cancellationToken)
    {
        var sql = """
            SELECT crew_id, owner_account_id, name, created_at, updated_at, version
            FROM crews
            WHERE crew_id = @crewId
            """;

        if (lockForUpdate)
        {
            sql += Environment.NewLine + "FOR UPDATE";
        }

        sql += ";";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("crewId", crewId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Crew.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetInt64(5));
    }

    private static async Task<IReadOnlyList<CrewMember>> GetCrewMembersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid crewId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT crew_id, account_id, role, created_at, updated_at, version
            FROM crew_members
            WHERE crew_id = @crewId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("crewId", crewId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var members = new List<CrewMember>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(MapCrewMember(reader));
        }

        return members;
    }

    private static CrewMember MapCrewMember(NpgsqlDataReader reader)
    {
        var role = Enum.Parse<CrewRole>(reader.GetString(2), ignoreCase: false);
        return CrewMember.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            role,
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetInt64(5));
    }

    private static void EnsureOwnerActor(Crew crew, Guid actorAccountId)
    {
        if (crew.OwnerAccountId != actorAccountId)
        {
            throw new CrewServiceException(CrewServiceFailureReason.Forbidden, "Only the crew owner can perform this operation.");
        }
    }
}
