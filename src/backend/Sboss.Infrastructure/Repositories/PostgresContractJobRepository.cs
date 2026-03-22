using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresContractJobRepository : IContractJobRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresContractJobRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ContractJob?> GetByIdAsync(Guid contractJobId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT contract_job_id, owning_account_id, current_state, created_at, updated_at, version
            FROM contract_jobs
            WHERE contract_job_id = @contractJobId;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("contractJobId", contractJobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapContractJob(reader);
    }

    internal static ContractJob MapContractJob(NpgsqlDataReader reader)
    {
        var state = Enum.Parse<ContractJobState>(reader.GetString(2), ignoreCase: false);
        return ContractJob.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            state,
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetInt64(5));
    }
}
