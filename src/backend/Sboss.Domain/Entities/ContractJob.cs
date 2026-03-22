namespace Sboss.Domain.Entities;

public sealed class ContractJob
{
    private ContractJob(
        Guid contractJobId,
        Guid owningAccountId,
        ContractJobState currentState,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        ContractJobId = contractJobId;
        OwningAccountId = owningAccountId;
        CurrentState = currentState;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public Guid ContractJobId { get; }
    public Guid OwningAccountId { get; }
    public ContractJobState CurrentState { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }

    public static ContractJob Create(Guid owningAccountId, DateTimeOffset createdAt)
    {
        if (owningAccountId == Guid.Empty)
        {
            throw new ArgumentException("Owning account ID is required.", nameof(owningAccountId));
        }

        return new ContractJob(Guid.NewGuid(), owningAccountId, ContractJobState.Draft, createdAt, createdAt, 1);
    }

    public static ContractJob Rehydrate(
        Guid contractJobId,
        Guid owningAccountId,
        ContractJobState currentState,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        if (contractJobId == Guid.Empty)
        {
            throw new ArgumentException("Contract job ID is required.", nameof(contractJobId));
        }

        if (owningAccountId == Guid.Empty)
        {
            throw new ArgumentException("Owning account ID is required.", nameof(owningAccountId));
        }

        if (!Enum.IsDefined(currentState))
        {
            throw new ArgumentOutOfRangeException(nameof(currentState), "Contract job state is invalid.");
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Updated timestamp cannot be earlier than created timestamp.");
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be greater than zero.");
        }

        return new ContractJob(contractJobId, owningAccountId, currentState, createdAt, updatedAt, version);
    }

    public ContractJob TransitionTo(ContractJobState targetState, DateTimeOffset transitionedAt)
    {
        if (!Enum.IsDefined(targetState))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState), "Target state is invalid.");
        }

        if (transitionedAt < CreatedAt)
        {
            throw new ArgumentException("Transition timestamp cannot be earlier than created timestamp.", nameof(transitionedAt));
        }

        if (transitionedAt < UpdatedAt)
        {
            throw new ArgumentException("Transition timestamp cannot be earlier than the current updated timestamp.", nameof(transitionedAt));
        }

        if (!IsLegalTransition(CurrentState, targetState))
        {
            throw new InvalidOperationException(
                $"Contract job transition from {CurrentState} to {targetState} is not allowed.");
        }

        return new ContractJob(
            ContractJobId,
            OwningAccountId,
            targetState,
            CreatedAt,
            transitionedAt,
            checked(Version + 1));
    }

    public static bool IsLegalTransition(ContractJobState currentState, ContractJobState targetState)
    {
        return (currentState, targetState) switch
        {
            (ContractJobState.Draft, ContractJobState.Open) => true,
            (ContractJobState.Open, ContractJobState.Accepted) => true,
            (ContractJobState.Accepted, ContractJobState.InProgress) => true,
            (ContractJobState.InProgress, ContractJobState.Completed) => true,
            (ContractJobState.InProgress, ContractJobState.Failed) => true,
            (ContractJobState.Open, ContractJobState.Cancelled) => true,
            (ContractJobState.Accepted, ContractJobState.Cancelled) => true,
            _ => false
        };
    }
}
