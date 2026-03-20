namespace Sboss.Infrastructure.Services;

public sealed class EconomyTransactionServiceException : Exception
{
    public EconomyTransactionServiceException(EconomyTransactionFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public EconomyTransactionFailureReason Reason { get; }
}
