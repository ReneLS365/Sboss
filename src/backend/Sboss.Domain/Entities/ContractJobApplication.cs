namespace Sboss.Domain.Entities;

public sealed class ContractJobApplication
{
    private ContractJobApplication(
        Guid contractJobApplicationId,
        Guid contractJobId,
        Guid applicantAccountId,
        ContractJobApplicationStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        ContractJobApplicationId = contractJobApplicationId;
        ContractJobId = contractJobId;
        ApplicantAccountId = applicantAccountId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid ContractJobApplicationId { get; }
    public Guid ContractJobId { get; }
    public Guid ApplicantAccountId { get; }
    public ContractJobApplicationStatus Status { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }

    public static ContractJobApplication Create(Guid contractJobId, Guid applicantAccountId, DateTimeOffset createdAt)
    {
        if (contractJobId == Guid.Empty)
        {
            throw new ArgumentException("Contract job ID is required.", nameof(contractJobId));
        }

        if (applicantAccountId == Guid.Empty)
        {
            throw new ArgumentException("Applicant account ID is required.", nameof(applicantAccountId));
        }

        return new ContractJobApplication(Guid.NewGuid(), contractJobId, applicantAccountId, ContractJobApplicationStatus.Submitted, createdAt, createdAt, 1);
    }

    public static ContractJobApplication Rehydrate(
        Guid contractJobApplicationId,
        Guid contractJobId,
        Guid applicantAccountId,
        ContractJobApplicationStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (contractJobApplicationId == Guid.Empty)
        {
            throw new ArgumentException("Contract job application ID is required.", nameof(contractJobApplicationId));
        }

        if (contractJobId == Guid.Empty)
        {
            throw new ArgumentException("Contract job ID is required.", nameof(contractJobId));
        }

        if (applicantAccountId == Guid.Empty)
        {
            throw new ArgumentException("Applicant account ID is required.", nameof(applicantAccountId));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Contract job application status is invalid.");
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new ContractJobApplication(contractJobApplicationId, contractJobId, applicantAccountId, status, createdAt, updatedAt, version);
    }

    public ContractJobApplication Withdraw(DateTimeOffset timestamp) => TransitionTo(ContractJobApplicationStatus.Withdrawn, timestamp);

    public ContractJobApplication Accept(DateTimeOffset timestamp) => TransitionTo(ContractJobApplicationStatus.Accepted, timestamp);

    public ContractJobApplication Reject(DateTimeOffset timestamp) => TransitionTo(ContractJobApplicationStatus.Rejected, timestamp);

    public ContractJobApplication TransitionTo(ContractJobApplicationStatus targetStatus, DateTimeOffset timestamp)
    {
        if (!Enum.IsDefined(targetStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(targetStatus), "Target status is invalid.");
        }

        if (timestamp < CreatedAt)
        {
            throw new ArgumentException("Transition timestamp cannot be earlier than created timestamp.", nameof(timestamp));
        }

        if (timestamp < UpdatedAt)
        {
            throw new ArgumentException("Transition timestamp cannot be earlier than the current updated timestamp.", nameof(timestamp));
        }

        if (!IsLegalTransition(Status, targetStatus))
        {
            throw new InvalidOperationException($"Contract job application transition from {Status} to {targetStatus} is not allowed.");
        }

        return new ContractJobApplication(
            ContractJobApplicationId,
            ContractJobId,
            ApplicantAccountId,
            targetStatus,
            CreatedAt,
            timestamp,
            checked(Version + 1));
    }

    public static bool IsLegalTransition(ContractJobApplicationStatus currentStatus, ContractJobApplicationStatus targetStatus)
    {
        return (currentStatus, targetStatus) switch
        {
            (ContractJobApplicationStatus.Submitted, ContractJobApplicationStatus.Withdrawn) => true,
            (ContractJobApplicationStatus.Submitted, ContractJobApplicationStatus.Accepted) => true,
            (ContractJobApplicationStatus.Submitted, ContractJobApplicationStatus.Rejected) => true,
            _ => false
        };
    }
}
