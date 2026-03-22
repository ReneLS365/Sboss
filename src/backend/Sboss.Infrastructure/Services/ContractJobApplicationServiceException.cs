namespace Sboss.Infrastructure.Services;

public sealed class ContractJobApplicationServiceException : Exception
{
    public ContractJobApplicationServiceException(ContractJobApplicationFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public ContractJobApplicationFailureReason Reason { get; }
}
