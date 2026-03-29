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
        if (ownerAccountId == Guid.Empty)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, "Owner account ID is required.");
        }

        var now = DateTimeOffset.UtcNow;
        Crew crew;
        try
        {
            crew = Crew.Create(ownerAccountId, name, now);
        }
        catch (ArgumentException exception)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, exception.Message);
        }

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

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var crew = await GetCrewAsync(connection, transaction, crewId, lockForUpdate: true, cancellationToken)
            ?? throw new CrewServiceException(CrewServiceFailureReason.NotFound, "Crew does not exist.");
        EnsureOwnerActor(crew, actorAccountId);

        var existingSettlement = await GetPayoutSettlementAsync(connection, transaction, crewId, normalizedIdempotencyKey, cancellationToken);
        CrewSplitResult split;
        CrewPayoutSettlement settlement;

        if (existingSettlement is not null)
        {
            EnsurePayoutReplayIntentMatches(existingSettlement, crewId, grossAmount, normalizedCurrencyCode, normalizedReason);
            split = new CrewSplitResult(
                crewId,
                existingSettlement.GrossAmount,
                existingSettlement.CrewShareRatioBps,
                existingSettlement.CrewShareAmount,
                existingSettlement.CompanyShareAmount,
                existingSettlement.MemberSettlements
                    .OrderBy(member => member.AccountId)
                    .Select(member => new CrewSplitMemberResult(member.AccountId, member.Role, member.RoleWeight, member.Amount))
                    .ToArray());
            settlement = existingSettlement;
        }
        else
        {
            var members = await GetCrewMembersAsync(connection, transaction, crew.CrewId, cancellationToken);
            split = CalculateSplit(crew.CrewId, grossAmount, members);
            settlement = await InsertPayoutSettlementAsync(
                connection,
                transaction,
                crew,
                split,
                normalizedCurrencyCode,
                normalizedIdempotencyKey,
                normalizedReason,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var payoutMutations = BuildPayoutMutations(settlement, split, crewId);

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
        if (value is null)
        {
            throw new CrewServiceException(CrewServiceFailureReason.InvalidRequest, $"{label} is required.");
        }
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

    private static IReadOnlyList<EconomyMutationRequest> BuildPayoutMutations(CrewPayoutSettlement settlement, CrewSplitResult split, Guid crewId)
    {
        var payoutMutations = new List<EconomyMutationRequest>(split.Members.Count + 1);
        foreach (var member in split.Members)
        {
            payoutMutations.Add(
                new EconomyMutationRequest(
                    member.AccountId,
                    settlement.CurrencyCode,
                    member.Amount,
                    $"{settlement.IdempotencyKey}:{member.AccountId:N}",
                    $"{settlement.Reason}:crew:{crewId:N}:member:{member.AccountId:N}"));
        }

        payoutMutations.Add(
            new EconomyMutationRequest(
                settlement.OwnerAccountId,
                settlement.CurrencyCode,
                split.CompanyShareAmount,
                $"{settlement.IdempotencyKey}:{settlement.OwnerAccountId:N}:company",
                $"{settlement.Reason}:crew:{crewId:N}:company"));

        return payoutMutations;
    }

    private static void EnsurePayoutReplayIntentMatches(
        CrewPayoutSettlement settlement,
        Guid crewId,
        long grossAmount,
        string currencyCode,
        string reason)
    {
        if (settlement.CrewId != crewId ||
            settlement.GrossAmount != grossAmount ||
            !string.Equals(settlement.CurrencyCode, currencyCode, StringComparison.Ordinal) ||
            !string.Equals(settlement.Reason, reason, StringComparison.Ordinal))
        {
            throw new CrewServiceException(
                CrewServiceFailureReason.Conflict,
                "Idempotency key is already bound to a different crew payout intent.");
        }
    }

    private static async Task<CrewPayoutSettlement?> GetPayoutSettlementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid crewId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string settlementSql = """
            SELECT crew_id, owner_account_id, idempotency_key, currency_code, reason, gross_amount, crew_share_ratio_bps, crew_share_amount, company_share_amount, created_at
            FROM crew_payout_settlements
            WHERE crew_id = @crewId AND idempotency_key = @idempotencyKey
            LIMIT 1
            FOR UPDATE;
            """;

        await using var settlementCommand = new NpgsqlCommand(settlementSql, connection, transaction);
        settlementCommand.Parameters.AddWithValue("crewId", crewId);
        settlementCommand.Parameters.AddWithValue("idempotencyKey", idempotencyKey);

        await using var settlementReader = await settlementCommand.ExecuteReaderAsync(cancellationToken);
        if (!await settlementReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var settlement = new CrewPayoutSettlement(
            settlementReader.GetGuid(0),
            settlementReader.GetGuid(1),
            settlementReader.GetString(2),
            settlementReader.GetString(3),
            settlementReader.GetString(4),
            settlementReader.GetInt64(5),
            settlementReader.GetInt32(6),
            settlementReader.GetInt64(7),
            settlementReader.GetInt64(8),
            settlementReader.GetFieldValue<DateTimeOffset>(9),
            Array.Empty<CrewPayoutMemberSettlement>());
        await settlementReader.CloseAsync();

        const string membersSql = """
            SELECT account_id, role, role_weight, amount
            FROM crew_payout_settlement_members
            WHERE crew_id = @crewId AND idempotency_key = @idempotencyKey
            ORDER BY account_id;
            """;

        await using var membersCommand = new NpgsqlCommand(membersSql, connection, transaction);
        membersCommand.Parameters.AddWithValue("crewId", crewId);
        membersCommand.Parameters.AddWithValue("idempotencyKey", idempotencyKey);

        var memberSettlements = new List<CrewPayoutMemberSettlement>();
        await using var membersReader = await membersCommand.ExecuteReaderAsync(cancellationToken);
        while (await membersReader.ReadAsync(cancellationToken))
        {
            memberSettlements.Add(new CrewPayoutMemberSettlement(
                membersReader.GetGuid(0),
                Enum.Parse<CrewRole>(membersReader.GetString(1), ignoreCase: false),
                membersReader.GetInt32(2),
                membersReader.GetInt64(3)));
        }

        return settlement with { MemberSettlements = memberSettlements };
    }

    private static async Task<CrewPayoutSettlement> InsertPayoutSettlementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Crew crew,
        CrewSplitResult split,
        string currencyCode,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken)
    {
        var createdAt = DateTimeOffset.UtcNow;
        const string settlementSql = """
            INSERT INTO crew_payout_settlements (
                crew_id,
                owner_account_id,
                idempotency_key,
                currency_code,
                reason,
                gross_amount,
                crew_share_ratio_bps,
                crew_share_amount,
                company_share_amount,
                created_at)
            VALUES (
                @crewId,
                @ownerAccountId,
                @idempotencyKey,
                @currencyCode,
                @reason,
                @grossAmount,
                @crewShareRatioBps,
                @crewShareAmount,
                @companyShareAmount,
                @createdAt);
            """;

        await using (var settlementCommand = new NpgsqlCommand(settlementSql, connection, transaction))
        {
            settlementCommand.Parameters.AddWithValue("crewId", crew.CrewId);
            settlementCommand.Parameters.AddWithValue("ownerAccountId", crew.OwnerAccountId);
            settlementCommand.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            settlementCommand.Parameters.AddWithValue("currencyCode", currencyCode);
            settlementCommand.Parameters.AddWithValue("reason", reason);
            settlementCommand.Parameters.AddWithValue("grossAmount", split.GrossAmount);
            settlementCommand.Parameters.AddWithValue("crewShareRatioBps", split.CrewShareRatioBps);
            settlementCommand.Parameters.AddWithValue("crewShareAmount", split.CrewShareAmount);
            settlementCommand.Parameters.AddWithValue("companyShareAmount", split.CompanyShareAmount);
            settlementCommand.Parameters.AddWithValue("createdAt", createdAt);
            await settlementCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string membersSql = """
            INSERT INTO crew_payout_settlement_members (
                crew_id,
                idempotency_key,
                account_id,
                role,
                role_weight,
                amount)
            VALUES (
                @crewId,
                @idempotencyKey,
                @accountId,
                @role,
                @roleWeight,
                @amount);
            """;

        foreach (var member in split.Members)
        {
            await using var membersCommand = new NpgsqlCommand(membersSql, connection, transaction);
            membersCommand.Parameters.AddWithValue("crewId", crew.CrewId);
            membersCommand.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            membersCommand.Parameters.AddWithValue("accountId", member.AccountId);
            membersCommand.Parameters.AddWithValue("role", member.Role.ToString());
            membersCommand.Parameters.AddWithValue("roleWeight", member.RoleWeight);
            membersCommand.Parameters.AddWithValue("amount", member.Amount);
            await membersCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return new CrewPayoutSettlement(
            crew.CrewId,
            crew.OwnerAccountId,
            idempotencyKey,
            currencyCode,
            reason,
            split.GrossAmount,
            split.CrewShareRatioBps,
            split.CrewShareAmount,
            split.CompanyShareAmount,
            createdAt,
            split.Members.Select(member =>
                new CrewPayoutMemberSettlement(member.AccountId, member.Role, member.RoleWeight, member.Amount)).ToArray());
    }

    private sealed record CrewPayoutSettlement(
        Guid CrewId,
        Guid OwnerAccountId,
        string IdempotencyKey,
        string CurrencyCode,
        string Reason,
        long GrossAmount,
        int CrewShareRatioBps,
        long CrewShareAmount,
        long CompanyShareAmount,
        DateTimeOffset CreatedAt,
        IReadOnlyList<CrewPayoutMemberSettlement> MemberSettlements);

    private sealed record CrewPayoutMemberSettlement(
        Guid AccountId,
        CrewRole Role,
        int RoleWeight,
        long Amount);

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
