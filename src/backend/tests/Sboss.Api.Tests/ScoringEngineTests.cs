using Sboss.Domain.Entities;
using Sboss.Infrastructure.Services;

namespace Sboss.Api.Tests;

public sealed class ScoringEngineTests
{
    [Fact]
    public void Compute_ReturnsDeterministicResult_ForSameValidatedInput()
    {
        var engine = new ScoringEngine();
        var seed = CreateSeed();
        var validatedPlacements = new[] { true, true, false, true, true };

        var first = engine.Compute(seed, validatedPlacements);
        var second = engine.Compute(seed, validatedPlacements);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_DerivesComboAndPenalties_FromValidationOutcomes()
    {
        var engine = new ScoringEngine();
        var seed = CreateSeed();
        var validatedPlacements = new[] { true, true, false, true };

        var result = engine.Compute(seed, validatedPlacements);

        Assert.Equal(2, result.ComboMax);
        Assert.Equal(1, result.Penalties);
        Assert.Equal(75, result.StabilityPercent);
    }

    private static LevelSeed CreateSeed()
    {
        var now = DateTimeOffset.UtcNow;
        return LevelSeed.Create(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "seed-alpha",
            "urban",
            "template-1",
            "objective-1",
            "{}",
            parTimeMs: 120000,
            goldTimeMs: 90000,
            now);
    }
}
