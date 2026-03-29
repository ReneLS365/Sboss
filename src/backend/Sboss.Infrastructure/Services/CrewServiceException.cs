namespace Sboss.Infrastructure.Services;

public sealed class CrewServiceException : Exception
{
    public CrewServiceException(CrewServiceFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public CrewServiceFailureReason Reason { get; }
}
