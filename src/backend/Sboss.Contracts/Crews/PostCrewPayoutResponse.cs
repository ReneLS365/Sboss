namespace Sboss.Contracts.Crews;

public sealed record PostCrewPayoutResponse(
    Guid CrewId,
    string CurrencyCode,
    long GrossAmount,
    long CompanyShareAmount,
    long CrewShareAmount,
    IReadOnlyList<CrewMemberBreakdownResponse> Members);
