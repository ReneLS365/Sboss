using Sboss.Domain.Entities;

namespace Sboss.Infrastructure.Services;

public interface IScoringEngine
{
    ScoringResult Compute(LevelSeed levelSeed, IReadOnlyList<bool> acceptedPlacements);
}
