using Npgsql;
using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Repositories;

public interface IContractJobApplicationRepository
{
    Task<ContractJobApplication?> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<ContractJobApplication?> GetByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid applicationId, bool lockForUpdate, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContractJobApplication>> GetSubmittedApplicationsForJobAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid contractJobId, bool lockForUpdate, CancellationToken cancellationToken);
    Task<ContractJobApplication> CreateSubmittedApplicationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ContractJobApplication application, CancellationToken cancellationToken);
    Task<ContractJobApplication?> UpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ContractJobApplication currentApplication, ContractJobApplication updatedApplication, CancellationToken cancellationToken);
    Task<ContractJobApplicationMutationRecord?> GetMutationByIdempotencyKeyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid contractJobId, string mutationKind, string idempotencyKey, CancellationToken cancellationToken);
    Task InsertMutationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid contractJobApplicationId, Guid contractJobId, string mutationKind, string idempotencyKey, long resultingVersion, DateTimeOffset createdAt, CancellationToken cancellationToken);
}

public sealed record ContractJobApplicationMutationRecord(
    Guid ContractJobApplicationMutationId,
    Guid ContractJobApplicationId,
    Guid ContractJobId,
    string MutationKind,
    string IdempotencyKey,
    long ResultingVersion,
    DateTimeOffset CreatedAt);
