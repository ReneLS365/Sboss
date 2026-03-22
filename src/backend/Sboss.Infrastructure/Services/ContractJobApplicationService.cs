using System.Data;
using Npgsql;
using Sboss.Domain.Entities;
using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure.Services;

public sealed class ContractJobApplicationService : IContractJobApplicationService
{
    private const string UniqueViolationSqlState = "23505";
    private readonly NpgsqlDataSource _dataSource;
    private readonly IContractJobApplicationRepository _applicationRepository;

    public ContractJobApplicationService(NpgsqlDataSource dataSource, IContractJobApplicationRepository applicationRepository)
    {
        _dataSource = dataSource;
        _applicationRepository = applicationRepository;
    }

    public async Task<ContractJobApplicationMutationResult> SubmitApplicationAsync(SubmitContractJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeSubmitRequest(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(connection, transaction, normalized.ContractJobId, normalized.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            var replayApplication = await _applicationRepository.GetByIdAsync(connection, transaction, replay.ContractJobApplicationId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(replayApplication, true, null, null);
        }

        var job = await ContractJobStateMutationHelper.GetContractJobAsync(connection, transaction, normalized.ContractJobId, true, cancellationToken);
        if (job is null)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.NotFound, "Contract job does not exist.");
        }

        if (job.CurrentState != ContractJobState.Open)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, $"Contract job applications are only allowed while the job is Open. Current state: {job.CurrentState}.");
        }

        replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(connection, transaction, normalized.ContractJobId, normalized.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            var replayApplication = await _applicationRepository.GetByIdAsync(connection, transaction, replay.ContractJobApplicationId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(replayApplication, true, null, null);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var application = ContractJobApplication.Create(normalized.ContractJobId, normalized.ApplicantAccountId, timestamp);

        try
        {
            var saved = await _applicationRepository.CreateSubmittedApplicationAsync(connection, transaction, application, cancellationToken);
            await _applicationRepository.InsertMutationAsync(connection, transaction, saved.ContractJobApplicationId, saved.ContractJobId, "Submit", normalized.IdempotencyKey, saved.Version, timestamp, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(saved, false, null, null);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolationSqlState)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await LoadReplayOrThrowConflictAsync(normalized.ContractJobId, normalized.IdempotencyKey, "An active submitted application already exists for this applicant and contract job.", cancellationToken);
        }
    }

    public async Task<ContractJobApplicationMutationResult> WithdrawApplicationAsync(WithdrawContractJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeMutationRequest(request.ContractJobId, request.ApplicationId, request.IdempotencyKey);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(connection, transaction, normalized.ContractJobId, normalized.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            var replayApplication = await _applicationRepository.GetByIdAsync(connection, transaction, replay.ContractJobApplicationId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(replayApplication, true, null, null);
        }

        var application = await _applicationRepository.GetByIdAsync(connection, transaction, normalized.ApplicationId, true, cancellationToken);
        if (application is null || application.ContractJobId != normalized.ContractJobId)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.NotFound, "Contract job application does not exist.");
        }

        var timestamp = DateTimeOffset.UtcNow;
        ContractJobApplication updated;
        try
        {
            updated = application.Withdraw(timestamp);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, $"Contract job application transition from {application.Status} to Withdrawn is not allowed.");
        }

        replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(connection, transaction, normalized.ContractJobId, normalized.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            var replayApplication = await _applicationRepository.GetByIdAsync(connection, transaction, replay.ContractJobApplicationId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(replayApplication, true, null, null);
        }

        var saved = await _applicationRepository.UpdateAsync(connection, transaction, application, updated, cancellationToken);
        if (saved is null)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, "Contract job application write conflict prevented the withdraw mutation.");
        }

        try
        {
            await _applicationRepository.InsertMutationAsync(connection, transaction, saved.ContractJobApplicationId, saved.ContractJobId, "Withdraw", normalized.IdempotencyKey, saved.Version, timestamp, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolationSqlState)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await LoadReplayOrThrowConflictAsync(normalized.ContractJobId, normalized.IdempotencyKey, "Contract job application withdraw conflict detected.", cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ContractJobApplicationMutationResult(saved, false, null, null);
    }

    public async Task<ContractJobApplicationMutationResult> AcceptApplicationAsync(AcceptContractJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeMutationRequest(request.ContractJobId, request.ApplicationId, request.IdempotencyKey);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(connection, transaction, normalized.ContractJobId, normalized.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            var replayApplication = await _applicationRepository.GetByIdAsync(connection, transaction, replay.ContractJobApplicationId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
            var replayJob = await ContractJobStateMutationHelper.GetContractJobAsync(connection, transaction, normalized.ContractJobId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job disappeared during replay lookup.");
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(replayApplication, true, replayJob.CurrentState, replayApplication.ContractJobApplicationId);
        }

        var lockedJob = await ContractJobStateMutationHelper.GetContractJobAsync(connection, transaction, normalized.ContractJobId, true, cancellationToken);
        if (lockedJob is null)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.NotFound, "Contract job does not exist.");
        }

        if (lockedJob.CurrentState != ContractJobState.Open)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, $"Contract job cannot accept applications while in state {lockedJob.CurrentState}.");
        }

        var submittedApplications = await _applicationRepository.GetSubmittedApplicationsForJobAsync(connection, transaction, normalized.ContractJobId, true, cancellationToken);
        var application = submittedApplications.SingleOrDefault(candidate => candidate.ContractJobApplicationId == normalized.ApplicationId);
        if (application is null)
        {
            var existingApplication = await _applicationRepository.GetByIdAsync(connection, transaction, normalized.ApplicationId, false, cancellationToken);
            if (existingApplication is null || existingApplication.ContractJobId != normalized.ContractJobId)
            {
                throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.NotFound, "Contract job application does not exist.");
            }

            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, $"Contract job application transition from {existingApplication.Status} to Accepted is not allowed.");
        }

        replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(connection, transaction, normalized.ContractJobId, normalized.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            var replayApplication = await _applicationRepository.GetByIdAsync(connection, transaction, replay.ContractJobApplicationId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
            var replayJob = await ContractJobStateMutationHelper.GetContractJobAsync(connection, transaction, normalized.ContractJobId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job disappeared during replay lookup.");
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(replayApplication, true, replayJob.CurrentState, replayApplication.ContractJobApplicationId);
        }

        var timestamp = DateTimeOffset.UtcNow;
        ContractJobApplication acceptedApplication;
        try
        {
            acceptedApplication = application.Accept(timestamp);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, $"Contract job application transition from {application.Status} to Accepted is not allowed.");
        }

        try
        {
            var savedAcceptedApplication = await _applicationRepository.UpdateAsync(connection, transaction, application, acceptedApplication, cancellationToken)
                ?? throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, "Contract job application write conflict prevented the accept mutation.");

            foreach (var competingApplication in submittedApplications.Where(candidate => candidate.ContractJobApplicationId != normalized.ApplicationId))
            {
                var rejectedApplication = competingApplication.Reject(timestamp);
                var savedRejectedApplication = await _applicationRepository.UpdateAsync(connection, transaction, competingApplication, rejectedApplication, cancellationToken);
                if (savedRejectedApplication is null)
                {
                    throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, "Concurrent contract job application mutation prevented rejecting competing submitted applications.");
                }
            }

            var savedJob = await ContractJobStateMutationHelper.ApplyTransitionAsync(connection, transaction, lockedJob, ContractJobState.Accepted, normalized.IdempotencyKey, timestamp, cancellationToken);
            await _applicationRepository.InsertMutationAsync(connection, transaction, savedAcceptedApplication.ContractJobApplicationId, savedAcceptedApplication.ContractJobId, "Accept", normalized.IdempotencyKey, savedAcceptedApplication.Version, timestamp, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContractJobApplicationMutationResult(savedAcceptedApplication, false, savedJob.CurrentState, savedAcceptedApplication.ContractJobApplicationId);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolationSqlState)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await LoadReplayOrThrowConflictAsync(normalized.ContractJobId, normalized.IdempotencyKey, "Contract job accept conflict detected.", cancellationToken, includeJobState: true);
        }
        catch (ContractJobTransitionServiceException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, exception.Message);
        }
    }

    private static SubmitContractJobApplicationRequest NormalizeSubmitRequest(SubmitContractJobApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ContractJobId == Guid.Empty)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.InvalidRequest, "Contract job ID is required.");
        }

        if (request.ApplicantAccountId == Guid.Empty)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.InvalidRequest, "Applicant account ID is required.");
        }

        var idempotencyKey = ContractJobStateMutationHelper.NormalizeTrimmedValue(
            request.IdempotencyKey,
            nameof(request.IdempotencyKey),
            "Idempotency key",
            message => new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.InvalidRequest, message));

        return request with { IdempotencyKey = idempotencyKey };
    }

    private static (Guid ContractJobId, Guid ApplicationId, string IdempotencyKey) NormalizeMutationRequest(Guid contractJobId, Guid applicationId, string idempotencyKey)
    {
        if (contractJobId == Guid.Empty)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.InvalidRequest, "Contract job ID is required.");
        }

        if (applicationId == Guid.Empty)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.InvalidRequest, "Application ID is required.");
        }

        var normalizedIdempotencyKey = ContractJobStateMutationHelper.NormalizeTrimmedValue(
            idempotencyKey,
            nameof(idempotencyKey),
            "Idempotency key",
            message => new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.InvalidRequest, message));

        return (contractJobId, applicationId, normalizedIdempotencyKey);
    }

    private async Task<ContractJobApplicationMutationResult> LoadReplayOrThrowConflictAsync(
        Guid contractJobId,
        string idempotencyKey,
        string fallbackMessage,
        CancellationToken cancellationToken,
        bool includeJobState = false)
    {
        await using var replayConnection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var replayTransaction = await replayConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var replay = await _applicationRepository.GetMutationByIdempotencyKeyAsync(replayConnection, replayTransaction, contractJobId, idempotencyKey, cancellationToken);
        if (replay is null)
        {
            throw new ContractJobApplicationServiceException(ContractJobApplicationFailureReason.Conflict, fallbackMessage);
        }

        var replayApplication = await _applicationRepository.GetByIdAsync(replayConnection, replayTransaction, replay.ContractJobApplicationId, false, cancellationToken)
            ?? throw new InvalidOperationException("Contract job application disappeared during replay lookup.");
        ContractJobState? replayJobState = null;
        if (includeJobState)
        {
            var replayJob = await ContractJobStateMutationHelper.GetContractJobAsync(replayConnection, replayTransaction, contractJobId, false, cancellationToken)
                ?? throw new InvalidOperationException("Contract job disappeared during replay lookup.");
            replayJobState = replayJob.CurrentState;
        }

        await replayTransaction.CommitAsync(cancellationToken);
        return new ContractJobApplicationMutationResult(replayApplication, true, replayJobState, replayApplication.ContractJobApplicationId);
    }
}
