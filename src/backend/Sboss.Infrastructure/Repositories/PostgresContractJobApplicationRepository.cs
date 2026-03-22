using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public sealed class PostgresContractJobApplicationRepository : IContractJobApplicationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresContractJobApplicationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ContractJobApplication?> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var application = await GetByIdAsync(connection, transaction, applicationId, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return application;
    }

    public async Task<ContractJobApplication?> GetByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid applicationId, bool lockForUpdate, CancellationToken cancellationToken)
    {
        var sql = """
            SELECT contract_job_application_id, contract_job_id, applicant_account_id, status, created_at, updated_at, version
            FROM contract_job_applications
            WHERE contract_job_application_id = @applicationId
            """;

        if (lockForUpdate)
        {
            sql += Environment.NewLine + "FOR UPDATE";
        }

        sql += ";";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("applicationId", applicationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapContractJobApplication(reader);
    }

    public async Task<IReadOnlyList<ContractJobApplication>> GetSubmittedApplicationsForJobAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid contractJobId, bool lockForUpdate, CancellationToken cancellationToken)
    {
        var sql = """
            SELECT contract_job_application_id, contract_job_id, applicant_account_id, status, created_at, updated_at, version
            FROM contract_job_applications
            WHERE contract_job_id = @contractJobId AND status = 'Submitted'
            ORDER BY created_at, contract_job_application_id
            """;

        if (lockForUpdate)
        {
            sql += Environment.NewLine + "FOR UPDATE";
        }

        sql += ";";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("contractJobId", contractJobId);

        var applications = new List<ContractJobApplication>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applications.Add(MapContractJobApplication(reader));
        }

        return applications;
    }

    public async Task<ContractJobApplication> CreateSubmittedApplicationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ContractJobApplication application, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO contract_job_applications (
                contract_job_application_id,
                contract_job_id,
                applicant_account_id,
                status,
                created_at,
                updated_at,
                version)
            VALUES (
                @applicationId,
                @contractJobId,
                @applicantAccountId,
                @status,
                @createdAt,
                @updatedAt,
                @version)
            RETURNING contract_job_application_id, contract_job_id, applicant_account_id, status, created_at, updated_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("applicationId", application.ContractJobApplicationId);
        command.Parameters.AddWithValue("contractJobId", application.ContractJobId);
        command.Parameters.AddWithValue("applicantAccountId", application.ApplicantAccountId);
        command.Parameters.AddWithValue("status", application.Status.ToString());
        command.Parameters.AddWithValue("createdAt", application.CreatedAt);
        command.Parameters.AddWithValue("updatedAt", application.UpdatedAt);
        command.Parameters.AddWithValue("version", application.Version);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapContractJobApplication(reader);
    }

    public async Task<ContractJobApplication?> UpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ContractJobApplication currentApplication, ContractJobApplication updatedApplication, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE contract_job_applications
            SET status = @status,
                updated_at = @updatedAt,
                version = @newVersion
            WHERE contract_job_application_id = @applicationId
              AND version = @expectedVersion
            RETURNING contract_job_application_id, contract_job_id, applicant_account_id, status, created_at, updated_at, version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("status", updatedApplication.Status.ToString());
        command.Parameters.AddWithValue("updatedAt", updatedApplication.UpdatedAt);
        command.Parameters.AddWithValue("newVersion", updatedApplication.Version);
        command.Parameters.AddWithValue("applicationId", currentApplication.ContractJobApplicationId);
        command.Parameters.AddWithValue("expectedVersion", currentApplication.Version);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapContractJobApplication(reader);
    }

    public async Task<ContractJobApplicationMutationRecord?> GetMutationByIdempotencyKeyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid contractJobId, string idempotencyKey, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT contract_job_application_mutation_id, contract_job_application_id, contract_job_id, mutation_kind, idempotency_key, resulting_version, created_at
            FROM contract_job_application_mutations
            WHERE contract_job_id = @contractJobId AND idempotency_key = @idempotencyKey
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ContractJobApplicationMutationRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6));
    }

    public async Task InsertMutationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid contractJobApplicationId, Guid contractJobId, string mutationKind, string idempotencyKey, long resultingVersion, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO contract_job_application_mutations (
                contract_job_application_mutation_id,
                contract_job_application_id,
                contract_job_id,
                mutation_kind,
                idempotency_key,
                resulting_version,
                created_at)
            VALUES (
                @mutationId,
                @applicationId,
                @contractJobId,
                @mutationKind,
                @idempotencyKey,
                @resultingVersion,
                @createdAt);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("mutationId", Guid.NewGuid());
        command.Parameters.AddWithValue("applicationId", contractJobApplicationId);
        command.Parameters.AddWithValue("contractJobId", contractJobId);
        command.Parameters.AddWithValue("mutationKind", mutationKind);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("resultingVersion", resultingVersion);
        command.Parameters.AddWithValue("createdAt", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static ContractJobApplication MapContractJobApplication(NpgsqlDataReader reader)
    {
        return ContractJobApplication.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            Enum.Parse<ContractJobApplicationStatus>(reader.GetString(3), ignoreCase: false),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetInt64(6));
    }
}
