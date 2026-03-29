using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed record CrewSplitMemberResult(Guid AccountId, CrewRole Role, int RatioWeight, long Amount);

public sealed record CrewSplitResult(
    Guid CrewId,
    long GrossAmount,
    int CrewShareRatioBps,
    long CrewShareAmount,
    long CompanyShareAmount,
    IReadOnlyList<CrewSplitMemberResult> Members);
