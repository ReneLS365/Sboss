namespace Sboss.Contracts.Commands;

public sealed record CommandValidationResult(bool Accepted, string Code, string Message)
{
    public static CommandValidationResult Accept() =>
        new(true, "accepted", "Placement intent accepted.");

    public static CommandValidationResult Reject(string code, string message) =>
        new(false, code, message);
}
