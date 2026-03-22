namespace Sboss.Infrastructure.Services;

public sealed class ContractJobTransitionServiceException : Exception
{
    public ContractJobTransitionServiceException(ContractJobTransitionFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public ContractJobTransitionFailureReason Reason { get; }
}
