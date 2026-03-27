namespace Sboss.Infrastructure.Services;

public sealed record ScoringResult(
    int Score,
    int ComboMax,
    int StabilityPercent,
    int ClearTimeMs,
    int Penalties);
