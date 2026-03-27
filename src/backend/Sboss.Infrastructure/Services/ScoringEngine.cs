using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public sealed class ScoringEngine : IScoringEngine
{
    public ScoringResult Compute(LevelSeed levelSeed, IReadOnlyList<bool> acceptedPlacements)
    {
        ArgumentNullException.ThrowIfNull(levelSeed);
        ArgumentNullException.ThrowIfNull(acceptedPlacements);

        if (acceptedPlacements.Count == 0)
        {
            throw new ArgumentException("At least one validated placement is required.", nameof(acceptedPlacements));
        }

        var acceptedCount = 0;
        var rejectedCount = 0;
        var runningCombo = 0;
        var comboMax = 0;

        foreach (var accepted in acceptedPlacements)
        {
            if (accepted)
            {
                acceptedCount++;
                runningCombo++;
                comboMax = Math.Max(comboMax, runningCombo);
            }
            else
            {
                rejectedCount++;
                runningCombo = 0;
            }
        }

        var total = acceptedCount + rejectedCount;
        var stabilityPercent = total == 0 ? 0 : (int)Math.Round((acceptedCount * 100d) / total, MidpointRounding.AwayFromZero);
        var clearTimeMs = Math.Max(levelSeed.GoldTimeMs, levelSeed.ParTimeMs - (acceptedCount * 250) + (rejectedCount * 500));
        var penalties = rejectedCount;
        var timeBonus = Math.Max(0, levelSeed.ParTimeMs - clearTimeMs) / 100;

        var score = Math.Max(
            0,
            (acceptedCount * 100)
            + (comboMax * 50)
            + (stabilityPercent * 5)
            + timeBonus
            - (penalties * 75));

        return new ScoringResult(score, comboMax, stabilityPercent, clearTimeMs, penalties);
    }
}
