namespace Sboss.Contracts.Crews;

public sealed record GetCrewSplitPreviewResponse(
    Guid CrewId,
    long GrossAmount,
    int CrewShareRatioBps,
    long CrewShareAmount,
    long CompanyShareAmount,
    IReadOnlyList<CrewMemberBreakdownResponse> Members);
